using JobTracker.Models;

namespace JobTracker.Repositories
{
    public interface ICompanyRepository
    {
        Task<IEnumerable<Company>> GetAllCompaniesAsync();
        Task<Company?> GetCompanyByIdAsync(Guid id);
        Task<Company> CreateCompanyAsync(Company company);
        Task UpdateCompanyAsync(Company company);
        Task DeleteCompanyAsync(Guid id);
    }
}