using DerivedFactoryMemoryLeak;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace IntegrationTests {
    public class DerivedFactoryTests {

        [Fact]
        public async Task DerivedFactoryTest_WithDisposableSingletonServices_AllShouldBeDisposed() {
            // Arrange
            var factory = new WebApplicationFactory<Startup>();
            var firstDerived = factory
                .WithWebHostBuilder(builder => {
                    builder.ConfigureTestServices(services => {
                        services.AddSingleton((_) => new DisposableService());
                    });
                });

            var secondDerived = firstDerived.WithWebHostBuilder(builder => {
                builder.ConfigureTestServices(services => {
                    services.AddSingleton((_) => new DisposableService());
                });
            });

            var allServices = new List<DisposableService>();
            var derivedFactories = new List<WebApplicationFactory<Startup>> { firstDerived, secondDerived };

            foreach (var derivedFactory in derivedFactories) {
                var client = derivedFactory.CreateClient();

                // Act
                var response = await client.GetAsync("/");

                // Assert
                Assert.Equal("Hello World!", await response.Content.ReadAsStringAsync());

                // explicitly resolve services from container so those get constructed
                var services = derivedFactory.Services.GetServices<DisposableService>();
                allServices.AddRange(services.ToList());
            }

            // expicitly dispose top-level factory
            factory.Dispose();

            // Total amount of DisposableService instances should be 3
            // 1 instance from first derived and 2 more instances from second derived factory
            Assert.Equal(3, allServices.Count);

            // and all 3 should be disposed when ancestor WebApplicationFactory is disposed
            var totalDisposed = allServices.Count(s => s.DisposedHasBeenCalled);
            Assert.Equal(3, totalDisposed);
        }
    }

    public class DisposableService : IDisposable {
        public bool DisposedHasBeenCalled { get; private set; }

        public void Dispose() {
            DisposedHasBeenCalled = true;
        }
    }
}
