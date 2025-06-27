using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Square.Connect.Model;
using SquareAccess.Models;

namespace SquareAccessTests.Mocks
{
    public class FakeOrdersService
    {
        public List<int> CallCounts { get; } = new List<int>();

        public async Task<SquareOrdersBatch> MockGetOrdersWithRelatedDataMethod(SearchOrdersRequest request)
        {
            CallCounts.Add(request.LocationIds.Count());
            return await Task.FromResult(new SquareOrdersBatch
            {
                Orders = new List<SquareOrder> { new SquareOrder() },
                Cursor = null
            });
        }
    }
}