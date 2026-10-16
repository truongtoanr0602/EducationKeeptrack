# Domain docs

## Layout

Use one domain context for the project:

- `GLOSSARY.md` at the project root holds shared domain terminology.
- `docs/adr/` holds architecture decision records.

## Consumer rules

Before exploring or changing the project, read the glossary and ADRs relevant to the task. If these documents do not exist, proceed silently; create them when terminology or architectural decisions need recording, using the domain-modeling workflow.

Use defined domain terms consistently in specs, issues, tests and implementation discussions. When a needed term is missing, identify the gap rather than silently assigning a conflicting meaning.

If a proposal conflicts with an existing ADR, identify that ADR and explain the proposed change before treating the new approach as agreed.
