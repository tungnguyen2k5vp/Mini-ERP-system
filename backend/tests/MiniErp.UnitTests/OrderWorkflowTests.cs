using MiniErp.Domain;
using MiniErp.Domain.Orders;

namespace MiniErp.UnitTests;

public sealed class OrderWorkflowTests
{
    [Fact]
    public void Header_edit_is_guarded_and_create_fingerprint_is_write_once()
    {
        var order = new TradeOrder();
        var partner = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var expected = new DateOnly(2026, 12, 1);
        order.FreezeCreatePayload("original");
        order.FreezeCreatePayload("replacement");
        Assert.Equal("original", order.CreatePayloadHash);
        order.Edit(partner, warehouse, "Changed", expected);
        Assert.Equal(partner, order.PartnerId);
        Assert.Equal(warehouse, order.WarehouseId);
        Assert.Equal(expected, order.ExpectedDate);
        order.Submit();
        Assert.Throws<ErpException>(() => order.Edit(Guid.NewGuid(), Guid.NewGuid(), null, null));
        Assert.Equal(partner, order.PartnerId);
        Assert.Equal("Changed", order.Note);
    }

    [Fact]
    public void Every_state_obeys_the_transition_matrix()
    {
        var actions = new (Action<TradeOrder> Run, OrderStatus[] Allowed, OrderStatus Result)[]
        {
            (x => x.Submit(), [OrderStatus.Draft], OrderStatus.PendingApproval),
            (x => x.Approve(), [OrderStatus.PendingApproval], OrderStatus.Approved),
            (x => x.Reject(), [OrderStatus.PendingApproval], OrderStatus.Rejected),
            (x => x.ReturnForRevision(), [OrderStatus.PendingApproval], OrderStatus.ReturnedForRevision),
            (x => x.Edit(), [OrderStatus.Draft, OrderStatus.ReturnedForRevision], OrderStatus.Draft),
            (x => x.Cancel(), [OrderStatus.Draft, OrderStatus.PendingApproval, OrderStatus.Approved,
                OrderStatus.Partial, OrderStatus.Cancelled, OrderStatus.ReturnedForRevision], OrderStatus.Cancelled)
        };
        foreach (var state in Enum.GetValues<OrderStatus>())
        foreach (var action in actions)
        {
            var order = new TradeOrder();
            typeof(TradeOrder).GetProperty(nameof(TradeOrder.Status))!.SetValue(order, state);
            if (action.Allowed.Contains(state)) { action.Run(order); Assert.Equal(action.Result, order.Status); }
            else { Assert.Throws<ErpException>(() => action.Run(order)); Assert.Equal(state, order.Status); }
        }
    }
}
