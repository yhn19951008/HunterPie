﻿using HunterPie.Core.Domain.Interfaces;
using HunterPie.Core.Extensions;
using HunterPie.Core.Game.Entity.Party;
using HunterPie.Core.Game.Enums;
using HunterPie.Core.Logger;
using HunterPie.Core.Native.IPC.Models.Common;
using HunterPie.Integrations.Datasources.Common.Entity.Party;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions;

namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Party;

public sealed class MHRParty : CommonParty, IUpdatable<EntityDamageData>, IUpdatable<MHRPartyMemberData>
{
    private readonly object _syncParty = new();
    private readonly Dictionary<int, MHRPartyMember> _partyMembers = new();
    private readonly Dictionary<int, MHRPartyMember> _partyMemberPets = new()
    {
        { 4, new MHRPartyMember(MemberType.Pet) }
    };
    private readonly Dictionary<string, MHRPartyMember> _partyMemberNameLookup = new();

    public const int MAX_PARTY_SIZE = 4;
    public override int Size { get; protected set; }

    public override int MaxSize
    {
        get => MAX_PARTY_SIZE;
        protected set => throw new NotSupportedException();
    }

    public override List<IPartyMember> Members
    {
        get
        {
            lock (_syncParty)
            {
                var members = _partyMembers.Values.ToList();
                var pets = _partyMemberPets.Values.ToList();

                members.AddRange(pets);

                return members.ToList<IPartyMember>();
            }
        }
    }

    public void Update(MHRPartyMemberData data)
    {
        if (string.IsNullOrEmpty(data.Name))
            return;

        lock (_syncParty)
        {
            if (!_partyMemberNameLookup.ContainsKey(data.Name))
                Add(data);

            if (!_partyMembers.ContainsKey(data.Index))
                return;

            IUpdatable<MHRPartyMemberData> updatable = _partyMembers[data.Index];
            updatable.Update(data);
        }
    }

    public void Update(EntityDamageData data)
    {
        lock (_syncParty)
        {
            Dictionary<int, MHRPartyMember>? localData = data.Entity.Type switch
            {
                EntityType.PLAYER => _partyMembers,
                EntityType.PET => _partyMemberPets,
                _ => null
            };

            if (localData is null)
                return;

            if (!localData.ContainsKey(data.Entity.Index))
                return;

            IUpdatable<EntityDamageData> updatable = localData[data.Entity.Index];
            updatable.Update(data);
        }
    }

    public void SetSize(int size) => Size = size;

    private void Add(MHRPartyMemberData data)
    {
        if (_partyMembers.ContainsKey(data.Index))
            Remove(data.Index);

        MHRPartyMember member = new();
        MHRPartyMember memberPet = new(MemberType.Pet);

        MHRPartyMemberData petData = data.ToPetData();

        _partyMembers.Add(data.Index, member);
        _partyMemberPets.Add(petData.Index, memberPet);
        _partyMemberNameLookup.Add(data.Name, member);

        IUpdatable<MHRPartyMemberData> updatable = member;
        IUpdatable<MHRPartyMemberData> updatablePet = memberPet;

        updatable.Update(data);
        updatablePet.Update(petData);

        Log.Debug("Added new player to party: id: {0} name: {1} weap: {2}, hash: {3:X}", data.Index, data.Name, data.WeaponId, updatable.GetHashCode());

        this.Dispatch(_onMemberJoin, member);
        this.Dispatch(_onMemberJoin, memberPet);
    }

    public void Remove(int memberIndex)
    {
        lock (_syncParty)
        {
            if (!_partyMembers.ContainsKey(memberIndex))
                return;

            int petIndex = memberIndex.ToPetId();

            MHRPartyMember member = _partyMembers[memberIndex];
            MHRPartyMember memberPet = _partyMemberPets[petIndex];

            _ = _partyMembers.Remove(memberIndex);
            _ = _partyMemberPets.Remove(petIndex);
            _ = _partyMemberNameLookup.Remove(member.Name);

            Log.Debug("Removed player: id: {0} name: {1} hash: {2:X}", memberIndex, member.Name, member.GetHashCode());

            this.Dispatch(_onMemberLeave, member);
            this.Dispatch(_onMemberLeave, memberPet);

            member.Dispose();
            memberPet.Dispose();
        }
    }

    public void Clear()
    {
        lock (_syncParty)
        {
            foreach (int index in _partyMembers.Keys.ToArray())
                Remove(index);

            // Reset pet damage
            IUpdatable<MHRPartyMemberData> updatable = _partyMemberPets[4];
            updatable.Update(new MHRPartyMemberData { Index = 4, MemberType = MemberType.Pet });
        }
    }

    public override void Dispose()
    {
        _partyMemberPets.Clear();
        base.Dispose();
    }
}