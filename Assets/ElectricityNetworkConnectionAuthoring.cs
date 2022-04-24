public class ElectricityNetworkConnectionAuthoring : ExchangeNetworkConnectorAuthoring<ElectricityNetwork, ElectricityPort, ElectricityNetworkConnection>
{
    protected override void SetSides(ref ElectricityNetworkConnection connection, GridItem.Side side)
    {
        connection.ConnectsAt = side;
    }
}
