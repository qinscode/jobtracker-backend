using JobTracker.Data;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Repositories
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly JobTrackerContext _context;

        public CompanyRepository(JobTrackerContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Company>> GetAllCompaniesAsync()
        {
            return await _context.Companies.ToListAsync();
        }

        public async Task<Company?> GetCompanyByIdAsync(Guid id)
        {
            return await _context.Companies.FindAsync(id);
        }

        public async Task<Company> CreateCompanyAsync(Company company)
        {
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();
            return company;
        }

        public async Task UpdateCompanyAsync(Company company)
        {
            _context.Entry(company).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCompanyAsync(Guid id)
        {
            var company = await _context.Companies.FindAsync(id);
            if (company != null)
            {
                _context.Companies.Remove(company);
                await _context.SaveChangesAsync();
            }
        }
    }
}