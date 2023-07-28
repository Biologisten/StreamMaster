﻿using StreamMasterApplication.ChannelGroups;
using StreamMasterApplication.ChannelGroups.Commands;
using StreamMasterApplication.ChannelGroups.Queries;

using StreamMasterDomain.Dto;

namespace StreamMasterApplication.Hubs;

public partial class StreamMasterHub : IChannelGroupHub
{
    public async Task AddChannelGroup(AddChannelGroupRequest request)
    {
        await _mediator.Send(request).ConfigureAwait(false);
    }

    public async Task DeleteChannelGroup(DeleteChannelGroupRequest request)
    {
        await _mediator.Send(request).ConfigureAwait(false);
    }

    public async Task<ChannelGroupDto?> GetChannelGroup(int id)
    {
        return await _mediator.Send(new GetChannelGroup(id)).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ChannelGroupDto>?> GetChannelGroups()
    {
        return await _mediator.Send(new GetChannelGroups()).ConfigureAwait(false);
    }

    public async Task SetChannelGroupsVisible(SetChannelGroupsVisibleRequest request)
    {
        await _mediator.Send(request).ConfigureAwait(false);
    }

    public async Task UpdateChannelGroup(UpdateChannelGroupRequest request)
    {
        await _mediator.Send(request).ConfigureAwait(false);
    }

    public async Task UpdateChannelGroupOrder(UpdateChannelGroupOrderRequest request)
    {
        await _mediator.Send(request).ConfigureAwait(false);
    }

    public async Task UpdateChannelGroups(UpdateChannelGroupsRequest request)
    {
        await _mediator.Send(request).ConfigureAwait(false);
    }
}
