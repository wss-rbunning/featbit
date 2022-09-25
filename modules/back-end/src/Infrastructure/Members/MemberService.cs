using Application.Bases.Exceptions;
using Application.Bases.Models;
using Application.Members;
using Application.Services;
using Domain.Groups;
using Domain.Members;
using Domain.Organizations;
using Domain.Policies;
using Domain.Users;
using Infrastructure.MongoDb;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Infrastructure.Members;

public class MemberService : IMemberService
{
    private readonly MongoDbClient _mongoDb;

    public MemberService(MongoDbClient mongoDb)
    {
        _mongoDb = mongoDb;
    }

    public async Task<Member> GetAsync(string organizationId, string memberId)
    {
        var users = _mongoDb.QueryableOf<User>();
        var organizationUsers = _mongoDb.QueryableOf<OrganizationUser>();

        var query =
            from user in users
            join organizationUser in organizationUsers
                on user.Id equals organizationUser.UserId
            where organizationUser.OrganizationId == organizationId && user.Id == memberId
            select new Member
            {
                Id = user.Id,
                Email = user.Email,
                InvitorId = organizationUser.InvitorId,
                InitialPassword = organizationUser.InitialPassword,
            };

        var member = await query.FirstOrDefaultAsync();
        if (member == null)
        {
            throw new EntityNotFoundException(nameof(Member), $"{organizationId}-{memberId}");
        }

        // get member groups
        member.Groups = await GetGroupsAsync(organizationId, new[] { member.Id });

        return member;
    }

    public async Task<PagedResult<Member>> GetListAsync(string organizationId, MemberFilter filter)
    {
        var users = _mongoDb.QueryableOf<User>();
        var organizationUsers = _mongoDb.QueryableOf<OrganizationUser>();

        var query =
            from user in users
            join organizationUser in organizationUsers
                on user.Id equals organizationUser.UserId
            where organizationUser.OrganizationId == organizationId
            select new Member
            {
                Id = user.Id,
                Email = user.Email,
                InvitorId = organizationUser.InvitorId,
                InitialPassword = organizationUser.InitialPassword,
            };

        // email or phoneNumber filter
        var searchText = filter.SearchText;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(x => x.Email.Contains(searchText, StringComparison.CurrentCultureIgnoreCase));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip(filter.PageIndex * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        // get member groups
        var memberIds = items.Select(x => x.Id);
        var groups = await GetGroupsAsync(organizationId, memberIds);
        foreach (var member in items)
        {
            member.Groups = groups.Where(x => x.MemberId == member.Id);
        }

        return new PagedResult<Member>(totalCount, items);
    }

    public async Task<List<MemberGroup>> GetGroupsAsync(string organizationId, IEnumerable<string> memberIds)
    {
        var groups = _mongoDb.QueryableOf<Group>();
        var groupMembers = _mongoDb.QueryableOf<GroupMember>();

        var query =
            from theGroup in groups
            join groupMember in groupMembers
                on theGroup.Id equals groupMember.GroupId
            where theGroup.OrganizationId == organizationId && memberIds.Contains(groupMember.MemberId)
            select new MemberGroup
            {
                Id = theGroup.Id,
                Name = theGroup.Name,
                Description = theGroup.Description,
                MemberId = groupMember.MemberId,
                IsGroupMember = true
            };

        var items = await query.ToListAsync();
        return items;
    }

    public async Task<PagedResult<MemberGroup>> GetGroupsAsync(
        string organizationId,
        string memberId,
        MemberGroupFilter filter)
    {
        var groups = _mongoDb.QueryableOf<Group>();
        var groupMembers = _mongoDb.QueryableOf<GroupMember>();

        // aws document db not support '$let' operator which means we cannot operate on 'allGroupMembers' in the main query
        var query =
            from theGroup in groups
            join groupMember in groupMembers 
                on theGroup.Id equals groupMember.GroupId
                into allGroupMembers
            where theGroup.OrganizationId == organizationId
            select new
            {
                theGroup.Id,
                theGroup.Name, 
                theGroup.Description,
                GroupMembers = allGroupMembers
            };
            
        if (!filter.GetAllGroups)
        {
            query = query.Where(x => x.GroupMembers.Any(y => y.MemberId == memberId));
        }

        var name = filter.Name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(x => x.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip(filter.PageIndex * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var memberGroups = items.Select(x => new MemberGroup
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description,
            MemberId = memberId,
            IsGroupMember = x.GroupMembers.Any(y => y.MemberId == memberId)
        }).ToList();

        return new PagedResult<MemberGroup>(totalCount, memberGroups);
    }

    public async Task<IEnumerable<Policy>> GetPoliciesAsync(string organizationId, string memberId)
    {
        // direct policies
        var policies = _mongoDb.QueryableOf<Policy>();
        var memberPolicies = _mongoDb.QueryableOf<MemberPolicy>();

        var directPolicyQuery =
            from policy in policies
            join memberPolicy in memberPolicies
                on policy.Id equals memberPolicy.PolicyId
            where memberPolicy.OrganizationId == organizationId && memberPolicy.MemberId == memberId
            select policy;

        // inherited policies
        var groups = _mongoDb.QueryableOf<Group>();
        var groupMembers = _mongoDb.QueryableOf<GroupMember>();
        var groupPolicies = _mongoDb.QueryableOf<GroupPolicy>();

        var inheritedPolicyQuery =
            from theGroup in groups
            join groupMember in groupMembers
                on theGroup.Id equals groupMember.GroupId
            join groupPolicy in groupPolicies
                on theGroup.Id equals groupPolicy.GroupId
            join policy in policies
                on groupPolicy.PolicyId equals policy.Id
            where groupMember.OrganizationId == organizationId && groupMember.MemberId == memberId
            select policy;

        var directPolicies = await directPolicyQuery.ToListAsync();
        var inheritedPolicies = await inheritedPolicyQuery.ToListAsync();

        // distinct by policy name
        var allPolicies = directPolicies.Concat(inheritedPolicies).GroupBy(x => x.Name).Select(x => x.First());
        return allPolicies;
    }
}