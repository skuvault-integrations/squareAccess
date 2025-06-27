using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Square.Connect.Model;
using SquareAccess.Exceptions;
using SquareAccess.Models;
using SquareAccess.Models.Items;
using SquareAccess.Services.Orders;
using SquareAccess.Shared;
using SquareAccessTests.Mocks;

namespace SquareAccessTests
{
    public class OrdersServiceTests : BaseTest
    {
        private ISquareOrdersService _ordersService;
        private bool _firstPage;
        private const string TestLocationId = "1GZS83Z3FC3Y3";
        private readonly DateTime startDateUtc = DateTime.UtcNow.AddDays(-700);
        private readonly DateTime endDateUtc = DateTime.UtcNow;

        [SetUp]
        public void Init()
        {
            _ordersService = new SquareOrdersService(Config, Credentials, new FakeLocationsService(TestLocationId),
                new FakeSquareItemsService());
        }

        [Test]
        public void GetOrdersAsync()
        {
            var result = _ordersService.GetOrdersAsync(startDateUtc, endDateUtc, CancellationToken.None).Result;

            Assert.That(result, Is.Not.Empty);
        }

        [Test]
        public void GetOrdersAsync_ByPage()
        {
            Config.OrdersPageSize = 2;

            var result = _ordersService.GetOrdersAsync(startDateUtc, endDateUtc, CancellationToken.None).Result;

            Assert.That(result.Count(), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task GetOrdersAsync_ShouldThrowException_WhenNoLocations()
        {
            // Arrange
            var service = new SquareOrdersService(
                Config,
                Credentials,
                new FakeLocationsService(() => Enumerable.Empty<SquareLocation>()),
                new FakeSquareItemsService()
            );

            // Act & Assert
            var exception = Assert.ThrowsAsync<SquareException>(async () =>
            {
                await service.GetOrdersAsync(startDateUtc, endDateUtc, CancellationToken.None);
            });

            Assert.That(
                exception?.Message,
                Does.Contain("No active locations. At least one is required to send SearchOrders request.")
            );
        }

        [Test]
        public void CreateSearchOrdersBody()
        {
            // Arrange
            var locations = new List<SquareLocation>
            {
                new SquareLocation
                {
                    Id = "i2o3jeo"
                },
                new SquareLocation
                {
                    Id = "aidfj23i"
                }
            };
            const string cursor = "23984ej2";
            const int ordersPerPage = 10;

            // Act
            var result = SquareOrdersService.CreateSearchOrdersBody(startDateUtc, endDateUtc, locations.AsEnumerable(),
                cursor, ordersPerPage);

            // Assert
            Assert.That(result.LocationIds, Is.EquivalentTo(locations.Select(l => l.Id)));
            Assert.That(result.Cursor, Is.EqualTo(cursor));
            Assert.That(result.Limit, Is.EqualTo(ordersPerPage));
            Assert.That(result.Query.Filter.DateTimeFilter.UpdatedAt.StartAt,
                Is.EqualTo(startDateUtc.FromUtcToRFC3339()));
            Assert.That(result.Query.Filter.DateTimeFilter.UpdatedAt.EndAt, Is.EqualTo(endDateUtc.FromUtcToRFC3339()));
        }

        [Test]
        public void CollectOrdersFromAllPagesAsync()
        {
            // Act
            const int ordersPerPage = 2;
            _firstPage = true;
            const string catalogObjectId = "asldfjlkj";
            const string quantity = "13";
            var recipient = new OrderFulfillmentRecipient
            {
                DisplayName = "Bubba"
            };
            var orders = new List<Order>
            {
                new Order("alkdsf23", "i2o3jeo")
                {
                    CreatedAt = "2019-01-03T05:07:51Z",
                    UpdatedAt = "2019-02-03T05:07:51Z",
                    LineItems = new List<OrderLineItem>
                    {
                        new OrderLineItem(null, null, quantity)
                        {
                            CatalogObjectId = catalogObjectId
                        }
                    },
                    Fulfillments = new List<OrderFulfillment>
                    {
                        new OrderFulfillment
                        {
                            ShipmentDetails = new OrderFulfillmentShipmentDetails
                            {
                                Recipient = recipient
                            }
                        }
                    }
                },
                new Order("23ik4lkj", "aidfj23i")
                {
                    CreatedAt = "2019-01-03T05:07:51Z",
                    UpdatedAt = "2019-02-03T05:07:51Z"
                }
            };
            const string sku = "testSku1";

            var items = new List<SquareItem>
            {
                new SquareItem
                {
                    VariationId = catalogObjectId,
                    Sku = sku
                }
            };

            // Act
            var result = SquareOrdersService.CollectOrdersFromAllPagesAsync(startDateUtc, endDateUtc,
                new List<SquareLocation>() { new SquareLocation { Id = TestLocationId } },
                (requestBody) => GetOrdersWithRelatedData(orders, items), ordersPerPage).Result.ToList();

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            var firstOrder = result.First();
            Assert.That(firstOrder.OrderId, Is.EqualTo(orders.First().Id));
            Assert.That(firstOrder.Recipient.Name, Is.EqualTo(recipient.DisplayName));
            var firstLineItem = firstOrder.LineItems.First();
            Assert.That(firstLineItem.Sku, Is.EqualTo(sku));
            Assert.That(firstLineItem.Quantity, Is.EqualTo(quantity));
            Assert.That(result.Skip(1).First().OrderId, Is.EqualTo(orders.Skip(1).First().Id));
        }

        [Test]
        public async Task CollectOrdersFromAllPagesAsync_ShouldBatchLocationsCorrectly()
        {
            // Arrange
            var locations = Enumerable.Range(1, 25).Select(i => new SquareLocation { Id = i.ToString() }).ToList();
            const int ordersPerPage = 5;

            var fakeOrdersService = new FakeOrdersService();

            // Act
            var result = await SquareOrdersService.CollectOrdersFromAllPagesAsync(
                startDateUtc,
                endDateUtc,
                locations,
                fakeOrdersService.MockGetOrdersWithRelatedDataMethod,
                ordersPerPage
            );

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(fakeOrdersService.CallCounts.Count, Is.EqualTo(3)); // 3 batches of locations
            Assert.That(fakeOrdersService.CallCounts[0], Is.EqualTo(10));
            Assert.That(fakeOrdersService.CallCounts[1], Is.EqualTo(10));
            Assert.That(fakeOrdersService.CallCounts[2], Is.EqualTo(5));
        }

        private async Task<SquareOrdersBatch> GetOrdersWithRelatedData(IEnumerable<Order> orders,
            IEnumerable<SquareItem> items)
        {
            SquareOrdersBatch result;

            if (_firstPage)
                result = new SquareOrdersBatch
                {
                    Orders = new List<SquareOrder>
                    {
                        orders.First().ToSvOrder(items)
                    },
                    Cursor = "fas23afs"
                };
            else
                result = new SquareOrdersBatch
                {
                    Orders = new List<SquareOrder>
                    {
                        orders.Skip(1).First().ToSvOrder(null)
                    }
                };

            _firstPage = false;

            return result;
        }
    }
}