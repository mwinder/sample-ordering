using System;
using System.Collections;
using System.Collections.Generic;

namespace Ordering.Api.Domain
{
    public class PurchaseOrderAggregate : IEnumerable<Event>
    {
        private readonly PurchaseOrderState state;
        private readonly List<Event> uncommittedEvents = new List<Event>();

        public PurchaseOrderAggregate(PurchaseOrderState state)
        {
            this.state = state;
        }

        public void Submit(string productCode, int quantity)
        {
            state.Status = PurchaseOrderStatus.Submitted;
            state.ProductCode = productCode;
            state.Quantity = quantity;

            Publish(new PurchaseOrderSubmitted { Id = state.Id, ProductCode = productCode, Quantity = quantity });
        }

        public void Approve()
        {
            state.Status = PurchaseOrderStatus.Approved;

            Publish(new PurchaseOrderApproved { Id = state.Id });
        }

        public void Decline(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                throw new InvalidOperationException("Reason is required");

            state.Status = PurchaseOrderStatus.Declined;

            Publish(new PurchaseOrderDeclined { Id = state.Id, Reason = reason });
        }

        private void Publish(Event e)
        {
            uncommittedEvents.Add(e);
        }

        public IEnumerator<Event> GetEnumerator()
        {
            return uncommittedEvents.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public PurchaseOrderState GetState()
        {
            return state;
        }
    }

    public class PurchaseOrderState
    {
        public int Id { get; set; }
        public PurchaseOrderStatus Status { get; set; }
        public string ProductCode { get; set; }
        public int Quantity { get; set; }
    }

    public enum PurchaseOrderStatus
    {
        Submitted,
        Approved,
        Declined
    }

    public class PurchaseOrderSubmitted : Event
    {
        public int Id { get; set; }
        public string ProductCode { get; set; }
        public int Quantity { get; set; }
    }

    public class PurchaseOrderApproved : Event
    {
        public int Id { get; set; }
    }

    public class PurchaseOrderDeclined : Event
    {
        public int Id { get; set; }
        public string Reason { get; set; }
    }

    public class Event { }

    public interface IPurchaseOrderRepository
    {
        PurchaseOrderAggregate GetById(int id);
        PurchaseOrderAggregate Create(int id);
        void Save(PurchaseOrderAggregate purchaseOrder);
    }

    public static class ServiceBus
    {
        public readonly static Queue<Event> Instance = new Queue<Event>();
    }

    public class SimplePurchaseOrderRepository : IPurchaseOrderRepository
    {
        private static readonly List<PurchaseOrderState> Store = new List<PurchaseOrderState>();
        public static readonly IPurchaseOrderRepository Instance = new SimplePurchaseOrderRepository(ServiceBus.Instance);

        static SimplePurchaseOrderRepository()
        {
            var quantity = new Random();
            for (int id = 1; id <= 20; id++)
            {
                var purchaseOrder = new PurchaseOrderAggregate(new PurchaseOrderState { Id = id });
                purchaseOrder.Submit(string.Format("Product-{0:00}", id), quantity.Next(10, 100));

                Instance.Save(purchaseOrder);
            }
        }

        private readonly Queue<Event> _bus;

        private SimplePurchaseOrderRepository(Queue<Event> bus)
        {
            _bus = bus;
        }

        public PurchaseOrderAggregate GetById(int id)
        {
            var purchaseOrderState = Store.Find(a => a.Id == id);
            return new PurchaseOrderAggregate(purchaseOrderState);
        }

        public PurchaseOrderAggregate Create(int id)
        {
            return new PurchaseOrderAggregate(new PurchaseOrderState { Id = id });
        }

        public void Save(PurchaseOrderAggregate purchaseOrder)
        {
            var purchaseOrderState = purchaseOrder.GetState();
            if (!Store.Exists(a => a.Id == purchaseOrderState.Id))
            {
                Store.Add(purchaseOrderState);
            }

            foreach (var @event in purchaseOrder)
            {
                _bus.Enqueue(@event);
            }
        }
    }
}