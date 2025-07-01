using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SquareAccess.Models;
using SquareAccess.Services.Locations;
using SquareAccess.Shared;

namespace SquareAccessTests.Mocks
{
	public class FakeLocationsService : ISquareLocationsService
	{
		private readonly string _locationId;
		private readonly Func<IEnumerable<SquareLocation>> _getLocations;

		public FakeLocationsService(string locationId)
		{
			_locationId = locationId;
			_getLocations = () => new[] { new SquareLocation { Id = _locationId, Active = true } };
		}

		public FakeLocationsService(Func<IEnumerable<SquareLocation>> getLocations)
		{
			_getLocations = getLocations;
		}

		public void Dispose()
		{
		}

		public Task<IEnumerable<SquareLocation>> GetActiveLocationsAsync(CancellationToken token, Mark mark)
		{
			return Task.FromResult(_getLocations().Where(l => l.Active));
		}

		public Task<IEnumerable<SquareLocation>> GetLocationsAsync(CancellationToken token, Mark mark)
		{
			return Task.FromResult(_getLocations());
		}
	}
}
