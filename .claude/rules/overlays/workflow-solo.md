# Workflow — Solo project (PR waived)

Compose with `core/workflow-core.md`. Use when you are the only contributor.

- A PR adds no review value with no second reviewer. You may squash-merge the
  `feature/` branch into the default branch yourself without opening one
  (e.g. `git switch master && git merge --squash feature/x`, then a single
  commit).
- The feature-branch and squash steps still stand — only the PR *mechanism* is
  waived, never the review itself: the owner reviews the finished branch and
  gives explicit approval before any squash-merge into the default branch.
- Switch to `workflow-team.md` the moment a second contributor joins.
