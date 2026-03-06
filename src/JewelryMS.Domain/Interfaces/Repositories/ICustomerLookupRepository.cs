using System.Collections.Generic;
using System.Threading.Tasks;
using JewelryMS.Domain.DTOs.Purchase;


namespace JewelryMS.Domain.Interfaces.Repositories;

/// <summary>
/// Lightweight customer search used in the purchase workflow.
/// Staff doesn't know a customer's UUID — they search by NID or phone.
/// </summary>
public interface ICustomerLookupRepository
{
    /// <summary>Find customers whose NID number contains the search term.</summary>
    Task<IEnumerable<CustomerSearchResult>> SearchByNidAsync(string nidNumber);

    /// <summary>Find customers whose contact number contains the search term.</summary>
    Task<IEnumerable<CustomerSearchResult>> SearchByContactAsync(string contactNumber);

    /// <summary>Combined search — tries NID first, then contact number.</summary>
    Task<IEnumerable<CustomerSearchResult>> SearchAsync(string searchTerm);
}