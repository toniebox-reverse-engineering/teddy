You are helping the user update the current release.

Follow these steps:

1. Get the latest git tag using `git tag --sort=-v:refname | head -1`
2. Get the commit hash that the tag points to using `git rev-parse <tag>` and store it for later analysis
3. Extract the version number from the tag (e.g., "v1.7.0" -> "1.7.0")
4. Delete the tag locally and remotely:
   - `git tag -d <tag>`
   - `git push origin :refs/tags/<tag>`
5. Analyze what changed since the stored commit hash:
   - Use `git log --oneline <hash>..HEAD` to see all commits since the tag
   - Use `git show --stat <commit_hash>` for each commit to understand the changes
   - Identify new features, bug fixes, and improvements
6. Update the corresponding CHANGELOG file (CHANGELOG-v{version}.md) based on your analysis:
   - Add new features to the "What's New" section
   - Add bug fixes to the "Bug Fixes" section
   - Update any other relevant sections
7. Stage all current changes including the changelog: `git add -A`
8. Commit with message: "Update release notes for <tag>" followed by a brief summary of what was added
9. Push the changes: `git push origin master`
10. Recreate the tag with the same name: `git tag -a <tag> -m "Release <tag>\n\n<brief description of main changes>"`
11. Push the tag to trigger the release pipeline: `git push origin <tag>`

After completing all steps, confirm to the user that the release has been updated and the pipeline should be triggered.