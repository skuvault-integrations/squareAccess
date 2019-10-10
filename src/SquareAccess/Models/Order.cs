﻿using System;
using System.Collections.Generic;
using System.Linq;
using Square.Connect.Model;
using SquareAccess.Models.Items;
using SquareAccess.Shared;
using Money = NMoneys.Money;

namespace SquareAccess.Models
{
	public class SquareOrder
	{
		public string OrderId { get; set; }
		public Money? OrderTotal { get; set; }
		public string CheckoutStatus { get; set; }
		public DateTime CreateDateUtc { get; set; }
		public DateTime UpdateDateUtc { get; set; }
		public IEnumerable< SquareOrderLineItem > LineItems { get; set; }
		public SquareOrderRecipient Recipient { get; set; }
	}

	public static class OrderExtensions
	{
		public static SquareOrder ToSvOrder( this Order order, IEnumerable< SquareItem > orderCatalogObjects )
		{
			return new SquareOrder
			{
				OrderId = order.Id,
				OrderTotal =  order.TotalMoney?.ToNMoney(),
				CheckoutStatus =  order.State,
				CreateDateUtc = order.CreatedAt.FromRFC3339ToUtc(),
				UpdateDateUtc = order.UpdatedAt.FromRFC3339ToUtc(),
				LineItems = order.LineItems?.ToSvOrderLineItems( orderCatalogObjects ).ToList(),
				Recipient = order.Fulfillments?.ToSvRecipient() ?? new SquareOrderRecipient()
			};
		}

		public static IEnumerable< SquareOrderLineItem > ToSvOrderLineItems( this IEnumerable< OrderLineItem > orderLineItems, IEnumerable< SquareItem > orderCatalogObjects )
		{
			if( orderCatalogObjects == null )
			{
				return new List< SquareOrderLineItem >();
			}
			return orderLineItems.Select( l => l.ToSvOrderLineItem( orderCatalogObjects.FirstOrDefault( c => c.VariationId == l.CatalogObjectId ) ) ).Where( l => l != null );
		}
	}

	public static class SquareOrderState
	{
		public const string Completed = "COMPLETED";
		public const string Open = "OPEN";
		public const string Cancelled = "CANCELED";
	}
}
