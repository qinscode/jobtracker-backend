using System;
using System.Linq;
using System.Threading.Tasks;
using JobTracker.Controllers;
using JobTracker.Data;
using JobTracker.Models;
using JobTracker.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobTracker.Tests.SystemTests
{
    public class JobApplicationSystemTest : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly JobTrackerContext _context;

        public JobApplicationSystemTest()
        {
            var services = new ServiceCollection();

            // Configure your test database
            services.AddDbContext<JobTrackerContext>(options =>
                options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

            services.AddScoped<IJobRepository, JobRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserJobRepository, UserJobRepository>();

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<JobTrackerContext>();
        }

        [Fact]
        public async Task UserCanApplyAndViewJobs()
        {
            // Arrange
            var jobRepo = _serviceProvider.GetRequiredService<IJobRepository>();
            var userRepo = _serviceProvider.GetRequiredService<IUserRepository>();
            var userJobRepo = _serviceProvider.GetRequiredService<IUserJobRepository>();

            var jobsController = new JobsController(jobRepo);
            var usersController = new UsersController(userRepo);
            var userJobsController = new UserJobsController(userJobRepo);

            // Create test jobs
            var job1 = new Job { JobTitle = "Software Developer", BusinessName = "Tech Co" };
            var job2 = new Job { JobTitle = "Data Analyst", BusinessName = "Data Inc" };
            var job3 = new Job { JobTitle = "Project Manager", BusinessName = "Project Co" };

            await jobsController.CreateJob(job1);
            await jobsController.CreateJob(job2);
            await jobsController.CreateJob(job3);

            // Create a test user
            var user = new User { Username = "testuser", Email = "test@example.com", PasswordHash = "hashedpassword" };
            var createUserResult = await usersController.CreateUser(user);
            var createdUser = (createUserResult.Result as CreatedAtActionResult).Value as User;

            // Act
            // User applies for two jobs
            await userJobsController.CreateUserJob(new UserJob { UserId = createdUser.Id, JobId = job1.Id, Status = UserJobStatus.Applied });
            await userJobsController.CreateUserJob(new UserJob { UserId = createdUser.Id, JobId = job2.Id, Status = UserJobStatus.Applied });

            // Get all user jobs
            var getUserJobsResult = await userJobsController.GetUserJobs();
            var userJobs = (getUserJobsResult.Result as OkObjectResult).Value as IEnumerable<UserJob>;

            // Assert
            Assert.NotNull(userJobs);
            Assert.Equal(2, userJobs.Count());
            Assert.All(userJobs, uj => Assert.Equal(createdUser.Id, uj.UserId));
            Assert.Contains(userJobs, uj => uj.JobId == job1.Id);
            Assert.Contains(userJobs, uj => uj.JobId == job2.Id);
            Assert.All(userJobs, uj => Assert.Equal(UserJobStatus.Applied, uj.Status));
            Assert.DoesNotContain(userJobs, uj => uj.JobId == job3.Id);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}