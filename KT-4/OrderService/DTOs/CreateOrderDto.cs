using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

/// <summary>
/// Запрос на создание заказа.
/// </summary>
/// <remarks>
/// В полной системе UserId и UserEmail берутся из JWT, выданного IdentityService.
/// Здесь IdentityService не реализован, поэтому они передаются в теле запроса.
/// </remarks>
/// <param name="UserId">Идентификатор покупателя.</param>
/// <param name="UserEmail">Email покупателя — на него уйдёт письмо о заказе.</param>
/// <param name="Items">Позиции заказа (минимум одна).</param>
/// <param name="ShippingAddress">Адрес доставки.</param>
/// <param name="Comment">Комментарий к заказу.</param>
public record CreateOrderDto(
    Guid UserId,

    [Required(ErrorMessage = "Email обязателен")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    string UserEmail,

    [Required]
    [MinLength(1, ErrorMessage = "Заказ должен содержать хотя бы одну позицию")]
    [MaxLength(50, ErrorMessage = "Не более 50 позиций в одном заказе")]
    List<CreateOrderItemDto> Items,

    [Required(ErrorMessage = "Адрес доставки обязателен")]
    ShippingAddressDto ShippingAddress,

    [MaxLength(1000)]
    string? Comment
);

/// <summary>Позиция в запросе на создание заказа.</summary>
/// <param name="ProductId">Идентификатор товара в CatalogService.</param>
/// <param name="Quantity">Количество, 1–1000.</param>
public record CreateOrderItemDto(
    Guid ProductId,

    [Range(1, 1000, ErrorMessage = "Количество должно быть от 1 до 1000")]
    int Quantity
);

/// <summary>Адрес доставки.</summary>
/// <param name="Street">Улица, дом, квартира.</param>
/// <param name="City">Город.</param>
/// <param name="ZipCode">Почтовый индекс (6 цифр).</param>
public record ShippingAddressDto(
    [Required(ErrorMessage = "Улица обязательна")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Улица должна быть от 3 до 200 символов")]
    string Street,

    [Required(ErrorMessage = "Город обязателен")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Город должен быть от 2 до 100 символов")]
    string City,

    [Required(ErrorMessage = "Индекс обязателен")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Индекс должен состоять из 6 цифр")]
    string ZipCode
);
