﻿using System.Threading;
using System.Threading.Tasks;
using SquareAccess.Models;
using SquareAccess.Shared;

namespace SquareAccess.Services.Customers
{
	public interface ISquareCustomersService
	{
		Task< SquareCustomer > GetCustomerByIdAsync( string customerId, CancellationToken token, Mark mark );
	}
}
