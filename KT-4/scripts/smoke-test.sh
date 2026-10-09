#!/usr/bin/env bash
# Сквозная проверка ShopHub.Microservices после `docker compose up -d --build`.
#
#   ./scripts/smoke-test.sh               — основные сценарии (~10 с)
#   ./scripts/smoke-test.sh --resilience  — плюс остановка CatalogService и RabbitMQ (~1,5 мин)
#
# Нужны: bash, curl, python3, docker compose. Завершается с кодом 1, если хоть одна проверка не прошла.

set -uo pipefail
cd "$(dirname "$0")/.."

CATALOG=${CATALOG_URL:-http://localhost:5002}
ORDER=${ORDER_URL:-http://localhost:5004}
NOTIFY=${NOTIFICATION_URL:-http://localhost:5006}
MAILPIT=${MAILPIT_URL:-http://localhost:8025}

IPHONE=3fa85f64-5717-4562-b3fc-2c963f66afa6
AIRPODS=5cb07a86-7939-4784-b5ce-4e185f88cfc8   # остаток 0
USER_ID=11111111-1111-1111-1111-111111111111
EMAIL="smoke-$(date +%s)@example.com"
ADDRESS='"shippingAddress":{"street":"ул. Ленина, 1","city":"Москва","zipCode":"101000"}'

PASS=0; FAIL=0
BODY=""; CODE=""

# Выполнить запрос: request МЕТОД URL [JSON]. Результат — в $CODE и $BODY.
request() {
  local method=$1 url=$2 data=${3:-}
  local out
  if [[ -n $data ]]; then
    out=$(curl -s -X "$method" "$url" -H 'Content-Type: application/json' -d "$data" -w $'\n%{http_code}')
  else
    out=$(curl -s -X "$method" "$url" -w $'\n%{http_code}')
  fi
  CODE=${out##*$'\n'}
  BODY=${out%$'\n'*}
}

# Проверить код ответа: expect ОЖИДАЕМЫЙ_КОД "описание"
expect() {
  if [[ $CODE == "$1" ]]; then
    printf '  \033[32m✔\033[0m %-62s %s\n' "$2" "$CODE"; PASS=$((PASS + 1))
  else
    printf '  \033[31m✘\033[0m %-62s %s (ожидали %s)\n' "$2" "$CODE" "$1"
    printf '      %s\n' "${BODY:0:300}"; FAIL=$((FAIL + 1))
  fi
}

# Проверить произвольное условие: check "описание" КОМАНДА...
check() {
  local title=$1; shift
  if "$@"; then
    printf '  \033[32m✔\033[0m %s\n' "$title"; PASS=$((PASS + 1))
  else
    printf '  \033[31m✘\033[0m %s\n' "$title"; FAIL=$((FAIL + 1))
  fi
}

# Достать поле из JSON в $BODY: json '["items"][0]["id"]'
json() { python3 -c "import json,sys; print(json.loads(sys.stdin.read())$1)" <<<"$BODY"; }

order_json() { # order_json PRODUCT_ID QTY
  echo "{\"userId\":\"$USER_ID\",\"userEmail\":\"$EMAIL\",\"items\":[{\"productId\":\"$1\",\"quantity\":$2}],$ADDRESS}"
}

# Ждать, пока уведомление по заказу не станет Sent (до $2 секунд)
wait_notification() {
  local order_id=$1 timeout=${2:-15}
  for _ in $(seq 1 "$timeout"); do
    request GET "$NOTIFY/api/notifications?orderId=$order_id"
    [[ $BODY == *'"status":"Sent"'* ]] && return 0
    sleep 1
  done
  return 1
}

echo "── 1. Health checks"
for url in "$CATALOG" "$ORDER" "$NOTIFY"; do
  request GET "$url/health"; expect 200 "GET $url/health"
  check "  статус Healthy" test "$(json '["status"]')" = "Healthy"
done

echo "── 2. CatalogService: CRUD + PostgreSQL + Redis"
request GET "$CATALOG/api/products";                 expect 200 "GET /api/products — список"
check "  в списке есть начальные товары" test "$(json '["totalCount"]')" -ge 2
request GET "$CATALOG/api/products?category=%D0%A1%D0%BC%D0%B0%D1%80%D1%82%D1%84%D0%BE%D0%BD%D1%8B"
expect 200 "GET /api/products?category=Смартфоны — фильтр"
request POST "$CATALOG/api/products" '{"name":"Smoke Test Phone","description":"Создан smoke-тестом","price":1000,"stock":10,"category":"Тест"}'
expect 201 "POST /api/products — создание"
PRODUCT_ID=$(json '["id"]')
request GET "$CATALOG/api/products/$PRODUCT_ID";     expect 200 "GET /api/products/{id} — чтение (кэш Redis)"
request PUT "$CATALOG/api/products/$PRODUCT_ID" '{"price":1500}'
expect 200 "PUT /api/products/{id} — изменение цены"
check "  цена обновилась, кэш сброшен" test "$(json '["price"]')" = "1500"
request POST "$CATALOG/api/products" '{"name":"ab","price":0,"stock":-1,"category":""}'
expect 400 "POST /api/products с ошибками — валидация"
request GET "$CATALOG/api/products/00000000-0000-0000-0000-000000000001"
expect 404 "GET несуществующего товара"

echo "── 3. OrderService → CatalogService (HTTP) → RabbitMQ → Notification/Catalog"
request POST "$ORDER/api/orders" "$(order_json "$PRODUCT_ID" 3)"
expect 201 "POST /api/orders — заказ на 3 шт."
ORDER_ID=$(json '["id"]')
check "  снимок цены из каталога: 3 × 1500 = 4500" test "$(json '["totalAmount"]')" = "4500.0"
request GET "$ORDER/api/orders/$ORDER_ID";          expect 200 "GET /api/orders/{id}"
request GET "$ORDER/api/orders?userId=$USER_ID";    expect 200 "GET /api/orders?userId= — история"
check "  NotificationService получил событие и отправил письмо" wait_notification "$ORDER_ID"
request GET "$CATALOG/api/products/$PRODUCT_ID"
check "  CatalogService получил событие и списал остаток: 10 → 7" test "$(json '["stock"]')" = "7"
request GET "$MAILPIT/api/v1/search?query=to:$EMAIL"
check "  письмо лежит в Mailpit" test "$(json '["messages_count"]')" -ge 1

echo "── 4. Ошибки OrderService"
request POST "$ORDER/api/orders" "$(order_json "$AIRPODS" 1)"
expect 409 "товара нет на складе → 409 Conflict"
request POST "$ORDER/api/orders" "$(order_json 00000000-0000-0000-0000-000000000042 1)"
expect 422 "товара нет в каталоге → 422 Unprocessable Entity"
request POST "$ORDER/api/orders" "{\"userId\":\"$USER_ID\",\"userEmail\":\"bad\",\"items\":[],$ADDRESS}"
expect 400 "невалидный запрос → 400 Bad Request"
request GET "$ORDER/api/orders/00000000-0000-0000-0000-000000000001"
expect 404 "несуществующий заказ → 404"

echo "── 5. Soft delete"
request DELETE "$CATALOG/api/products/$PRODUCT_ID"; expect 204 "DELETE /api/products/{id}"
request GET "$CATALOG/api/products/$PRODUCT_ID";    expect 404 "удалённый товар больше не отдаётся"
request GET "$ORDER/api/orders/$ORDER_ID"
check "  старый заказ сохранил снимок удалённого товара" test "$(json '["items"][0]["productName"]')" = "Smoke Test Phone"

if [[ ${1:-} == "--resilience" ]]; then
  echo "── 6. Устойчивость: CatalogService остановлен"
  docker compose stop catalog-service >/dev/null 2>&1
  for i in 1 2 3; do
    request POST "$ORDER/api/orders" "$(order_json "$IPHONE" 1)"
    expect 503 "заказ без каталога, попытка $i → 503 (retry → circuit breaker)"
  done
  request GET "$ORDER/health"
  check "  /health OrderService = Degraded (каталог недоступен, своя БД жива)" test "$(json '["status"]')" = "Degraded"
  docker compose start catalog-service >/dev/null 2>&1
  echo "     каталог запущен, ждём окончания BreakDuration (30 с)..."
  sleep 32
  request POST "$ORDER/api/orders" "$(order_json "$IPHONE" 1)"
  expect 201 "после восстановления каталога заказ снова создаётся"

  echo "── 7. Устойчивость: RabbitMQ остановлен (transactional outbox)"
  docker compose stop rabbitmq >/dev/null 2>&1
  request POST "$ORDER/api/orders" "$(order_json "$IPHONE" 1)"
  expect 201 "заказ создаётся без брокера — событие ждёт в outbox"
  OUTBOX_ORDER=$(json '["id"]')
  docker compose start rabbitmq >/dev/null 2>&1
  check "  после запуска RabbitMQ письмо всё равно доставлено" wait_notification "$OUTBOX_ORDER" 60
fi

echo
echo "Итого: $PASS успешно, $FAIL с ошибками"
[[ $FAIL -eq 0 ]]
