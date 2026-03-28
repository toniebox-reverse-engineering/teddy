You are helping the user create a new release with semantic versioning.

Follow these steps:

1. Get the latest git tag using `git tag --sort=-v:refname | head -1`
2. Extract the version number from the tag (e.g., "v1.7.0" -> "1.7.0")
3. Parse the semantic version into major.minor.patch components
4. Ask the user which component to bump:
   - major (x.0.0) - for breaking changes
   - minor (x.y.0) - for new features, backwards compatible
   - patch (x.y.z) - for bug fixes and minor changes
5. Calculate the new version number based on their choice:
   - major: increment major, reset minor and patch to 0
   - minor: keep major, increment minor, reset patch to 0
   - patch: keep major and minor, increment patch
6. Ask the user for release notes/changelog content for the new version
7. Create a new CHANGELOG file: CHANGELOG-v{new_version}.md with the provided content
8. Stage all current changes including the new changelog: `git add -A`
9. Commit with message: "Prepare release v{new_version}"
10. Push the changes: `git push origin master`
11. Create the new tag: `git tag -a v{new_version} -m "Release v{new_version}\n\n{summary of changes}"`
12. Push the tag to trigger the release pipeline: `git push origin v{new_version}`

After completing all steps, confirm to the user:
- The old version
- The new version
- That the release pipeline should be triggered