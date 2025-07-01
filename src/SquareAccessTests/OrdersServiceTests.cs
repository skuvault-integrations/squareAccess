using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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
		private readonly DateTime startDateUtc = DateTime.UtcNow.AddDays(-20);
		private readonly DateTime endDateUtc = DateTime.UtcNow;		

		[ SetUp ]
		public void Init()
		{
			this._ordersService = new SquareOrdersService( this.Config, this.Credentials, new FakeLocationsService( TestLocationId ), new FakeSquareItemsService() );
		}

		[ Test ]
		public void GetOrdersAsync()
		{						
			var result = _ordersService.GetOrdersAsync( startDateUtc, endDateUtc, CancellationToken.None ).Result;

			result.Should().NotBeEmpty();
		}

		[ Test ]
		public void GetOrdersAsync_ByPage()
		{			
			this.Config.OrdersPageSize = 2;

			var result = _ordersService.GetOrdersAsync( startDateUtc, endDateUtc, CancellationToken.None ).Result;

			result.Count().Should().BeGreaterOrEqualTo( 2 );
		}

		[Test]
		public async Task GetOrdersAsync_ShouldThrowSquareException_WhenNoLocations()
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

		[ Test ]
		public void CreateSearchOrdersBody()
		{						
			var locations = new List< SquareLocation >
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

			var result = SquareOrdersService.CreateSearchOrdersBody( startDateUtc, endDateUtc, locations.AsEnumerable(), cursor, ordersPerPage );

			result.LocationIds.Should().BeEquivalentTo( locations.Select( l => l.Id ) );
			result.Cursor.Should().Be( cursor );
			result.Limit.Should().Be( ordersPerPage );
			result.Query.Filter.DateTimeFilter.UpdatedAt.StartAt.Should().Be( startDateUtc.FromUtcToRFC3339() );
			result.Query.Filter.DateTimeFilter.UpdatedAt.EndAt.Should().Be( endDateUtc.FromUtcToRFC3339() );
		}

		[ Test ]
		public void CollectOrdersFromAllPagesAsync()
		{			
			const int ordersPerPage = 2;
			_firstPage = true;
			const string catalogObjectId = "asldfjlkj";
			const string quantity = "13";
			var recipient = new OrderFulfillmentRecipient
			{
				DisplayName = "Bubba"
			};
			var orders = new List< Order >
			{
				new Order( "alkdsf23", "i2o3jeo" )
				{
					CreatedAt = "2019-01-03T05:07:51Z",
					UpdatedAt = "2019-02-03T05:07:51Z",
					LineItems = new List< OrderLineItem >
					{
						new OrderLineItem( null, null, Quantity: quantity )
						{
							CatalogObjectId = catalogObjectId,
						}
					},
					Fulfillments = new List< OrderFulfillment >
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
				new Order( "23ik4lkj", "aidfj23i" )
				{
					CreatedAt = "2019-01-03T05:07:51Z",
					UpdatedAt = "2019-02-03T05:07:51Z"
				}
			};
			const string sku = "testSku1";
			
			var items = new List< SquareItem >
			{
				new SquareItem
				{
					VariationId = catalogObjectId,
					Sku = sku
				}
			};

			var result = SquareOrdersService.CollectOrdersFromAllPagesAsync( startDateUtc, endDateUtc, new List< SquareLocation > { new SquareLocation { Id = TestLocationId } }, 
				( requestBody ) => GetOrdersWithRelatedData( orders, items ), ordersPerPage ).Result.ToList();

			result.Count.Should().Be( 2 );
			var firstOrder = result.First();
			firstOrder.OrderId.Should().BeEquivalentTo( orders.First().Id );
			firstOrder.Recipient.Name.Should().BeEquivalentTo( recipient.DisplayName );
			var firstLineItem = firstOrder.LineItems.First();
			firstLineItem.Sku.Should().Be( sku );
			firstLineItem.Quantity.Should().Be( quantity );
			result.Skip( 1 ).First().OrderId.Should().BeEquivalentTo( orders.Skip( 1 ).First().Id );
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

		private async Task< SquareOrdersBatch > GetOrdersWithRelatedData( IEnumerable< Order > orders, IEnumerable< SquareItem > items)
		{
			SquareOrdersBatch result;
			
			if( _firstPage )
			{
				result = new SquareOrdersBatch
				{
					Orders = new List< SquareOrder >
					{
						orders.First().ToSvOrder( items )
					},
					Cursor =  "fas23afs"
				};
			}
			else
			{
				result = new SquareOrdersBatch
				{
					Orders = new List< SquareOrder >
					{
						orders.Skip( 1 ).First().ToSvOrder( null )
					}
				};
			}

			_firstPage = false;

			return result;
		}
	}
}