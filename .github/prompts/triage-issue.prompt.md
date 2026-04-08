---
name: triage-issue
description: Triage a new GitHub issue — categorize, label, request info, and link related items.
argument-hint: <issue number>
agent: agent
tools: ['github/search_issues', 'read/readFile', 'codebase/search']
---

Triage GitHub issue #${input:issue} in `dotnet/SqlClient`.

> **IMPORTANT**: This prompt is reference material only. Do NOT post comments,
> do NOT output results for individual sections. Gather all information silently
> and return findings to the calling workflow.

**GitHub Project**: https://github.com/orgs/dotnet/projects/588/ — All issues are tracked here. After triaging, update the issue's project fields.

Use the reference sections below to gather information. Do NOT take any output
actions (no comments, no reports) — just collect findings and return them.

### Reference: Issue Type Classification
Determine which issue template was used:
- `ISSUE_TEMPLATE/bug-report.md` → Bug
- `ISSUE_TEMPLATE/feature_request.md` → Feature
- No template / sub-issue → Task
- High-level work with sub-issues → Epic

### Reference: Completeness Checklist

**For Bugs**, check for:
- .NET version and target framework
- Microsoft.Data.SqlClient version
- SQL Server version (if relevant)
- OS and platform
- Repro steps or minimal repro code
- Expected vs actual behavior
- Stack trace or error message

**For Features**, check for:
- Clear problem statement / motivation
- Proposed solution or API shape
- Use case / scenario description

If any required details are missing, add the label `Needs more info :information_source:`.

### Reference: Available Area Labels (use exact names)
- `Area\Engineering` — Build system, CI/CD, project infrastructure
- `Area\Connection Pooling` — Pool behavior, timeouts, pool size
- `Area\AKV Provider` — Always Encrypted Azure Key Vault provider
- `Area\Json` — JSON data type support
- `Area\Managed SNI` — Managed SNI codebase (network layer)
- `Area\Native SNI` — Native SNI codebase (network layer)
- `Area\Sql Bulk Copy` — SqlBulkCopy operations
- `Area\Netcore` — Issues specific to .NET runtime / netcore folder
- `Area\Netfx` — Issues specific to .NET Framework / netfx folder
- `Area\Tests` — Test code / test projects
- `Area\Documentation` — Documentation and samples
- `Area\Azure Connectivity` — Azure connectivity issues
- `Area\Vector` — Vector feature
- `Area\Async` — Async operations

### Reference: Available Status Labels
- `Triage Needed :new:` — For new issues needing initial review
- `Needs more info :information_source:` — Missing required details
- `Regression :boom:` — Regressions from earlier PRs
- `Performance :chart_with_upwards_trend:` — Performance-related concern
- `Repro Available :heavy_check_mark:` — Issue has repro steps provided

### Reference: GitHub Project Fields

| Field | Values | Guidance |
|-------|--------|----------|
| **Status** | `To Triage`, `Needs Response`, `Investigating`, `Waiting for customer`, `Backlog`, `In progress`, `In review`, `Done` | Set to `Investigating` if actionable, `Needs Response` if info is missing from reporter, `Waiting for customer` if awaiting external input, `Backlog` for valid lower-priority items |
| **Priority** | `P0`, `P1`, `P2`, `P3` | **P0**: Critical — data loss, security vulnerability, widespread breakage. **P1**: High — significant regression, blocking customer scenario. **P2**: Medium — important but has workaround, affects subset of users. **P3**: Low — minor issue, enhancement, nice-to-have |
| **Size** | `XS`, `S`, `M`, `L`, `XL` | Estimate effort: **XS**: trivial fix (<1h). **S**: small, well-scoped (<1 day). **M**: moderate, may touch multiple files (1-3 days). **L**: significant, cross-cutting (1-2 weeks). **XL**: large feature or architectural change (2+ weeks) |

### Reference: Duplicate Search
- Search for existing issues with similar keywords: `repo:dotnet/SqlClient <key terms>`
- Note any duplicates or related issues found — these will be linked in the triage comment.

### Reference: Affected Code Area
- Source files are in `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/`
- Key components: SqlConnection, TdsParser, ConnectionPool, SqlCommand
- Check if the issue is platform-specific (Windows-only, Unix-only, .NET Framework-only)

## 9. Summary
Output a brief triage summary:
- **Type**: Bug / Feature / Task / Epic
- **Area**: Which component(s) affected
- **Labels applied**: List of labels
- **Project fields**: Status, Priority, Size values set
- **Missing info**: What additional info is needed (if any)
- **Related issues**: Links to related issues or PRs
- **Severity assessment**: Low / Medium / High / Critical
