namespace RotaGame.Core;

public static class PeerMatching
{
    public static DeviceProfile Recognize(DeviceProfile profile, IReadOnlyList<ReceivedDevice> authenticatedFreshPeers)
    {
        if (profile.PartnerNodeId is not null) return profile;
        var opposite = profile.Role == DeviceRole.Player ? DeviceRole.Bridge : profile.Role == DeviceRole.Bridge ? DeviceRole.Player : DeviceRole.Unknown;
        var matches = authenticatedFreshPeers.Where(p => p.Frame.Role == opposite).Select(p => p.NodeId).Distinct().ToArray();
        return opposite != DeviceRole.Unknown && matches.Length == 1 ? profile with { PartnerNodeId = matches[0] } : profile;
    }
    public static DeviceFrame Outgoing(DeviceProfile profile, string authenticatedTargetNode, bool bridgeUsable, DeviceFrame frame) =>
        frame with { Measurements = profile.PartnerNodeId == authenticatedTargetNode ? frame.Measurements : null, PairRequested = profile.PartnerNodeId == authenticatedTargetNode, BridgeUsable = bridgeUsable };
}
