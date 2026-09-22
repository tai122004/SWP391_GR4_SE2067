using RWPM.Models.Common;
using System;
using System.ComponentModel.DataAnnotations;

public class Store : AuditableEntity, IActivatable
{
    [Key]
    public int StoreId { get; set; }

    [Required]
    [MaxLength(20)]
    public string StoreCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string StoreName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int MinAllowedDistanceMeters { get; set; } = 0;

    public int AllowedRadiusMeters { get; set; } = 100;

    public bool IsActive { get; set; } = true;
}