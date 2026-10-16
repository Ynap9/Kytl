using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ktl.be.shared.Constants.Documents;

namespace ktl.be.infrastructure.data.Persistence
{
    public class SignTemplateDbContextFactory : IDesignTimeDbContextFactory<SignTemplateDbContext>
    {
        public SignTemplateDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<SignTemplateDbContext>()
                .UseSqlite($"Data Source={SignTemplateConstants.DatabasePath}")
                .Options;

            return new SignTemplateDbContext(options);
        }
    }
}
