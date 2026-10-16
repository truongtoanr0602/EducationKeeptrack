# Issue tracker: GitHub

Issues and specs live in GitHub Issues at https://github.com/truongtoanr0602/EducationKeeptrack.

## Operations

Use the `gh` CLI. Pass `--repo truongtoanr0602/EducationKeeptrack` explicitly to repository-scoped commands, including when the workspace has no Git remote.

- Publish a spec or ticket: create a GitHub issue with a descriptive title. Write multiline content to a temporary UTF-8 file and pass it with `--body-file`.
- Read a ticket: `gh issue view <number> --repo truongtoanr0602/EducationKeeptrack --json number,title,body,labels,comments`.
- List tickets: `gh issue list --repo truongtoanr0602/EducationKeeptrack --state open --json number,title,labels`.
- Apply or remove a label: `gh issue edit <number> --repo truongtoanr0602/EducationKeeptrack --add-label <label>` or `--remove-label <label>`.
- Add an authorized comment: use `gh issue comment` with the explicit repository and `--body-file` for multiline content.
- Close an issue: use `gh issue close` with the explicit repository.

When a skill says "publish to the issue tracker", create a GitHub issue. When it says "fetch the relevant ticket", read the referenced GitHub issue. Use the role mappings in [triage-labels.md](triage-labels.md).

## Pull requests as a triage surface

**PRs as a request surface: no.**

## Parent issues and dependencies

Use GitHub sub-issues for parent/child relationships when supported. Otherwise, add `Part of #<parent>` to the child and a task-list link in the parent.

Use native GitHub issue dependencies when supported. For GitHub API operations, use the explicit repository API path and numeric database IDs where required. Otherwise, record `Blocked by: #<number>` in the issue body and inspect the referenced issues before treating it as unblocked.

For a wayfinder workflow, the map is an issue labelled `wayfinder:map`; children use `wayfinder:<type>` labels. Claim an open, unblocked, unassigned child by assigning it to the driving developer. Append the answer, close the child and update the map with a link to the decision.
