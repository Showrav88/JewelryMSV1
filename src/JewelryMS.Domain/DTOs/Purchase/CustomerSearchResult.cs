namespace JewelryMS.Domain.DTOs.Purchase
{
    public class CustomerSearchResult
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ContactNumber { get; set; }
        public string? NidNumber { get; set; }
        public string? Email { get; set; }
        public bool ActivityStatus { get; set; }
    }
}