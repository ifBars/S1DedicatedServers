# Contributing to DedicatedServerMod

Thank you for your interest in contributing to DedicatedServerMod! This document provides guidelines and instructions for contributors.

---

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](CODING_STANDARDS.md)
- [Pull Request Process](#pull-request-process)
- [Issue Reporting](#issue-reporting)
- [Community](#community)

---

## 🤝 Code of Conduct

### Our Pledge

We are committed to providing a friendly, safe, and welcoming environment for all contributors, regardless of level of experience, gender identity and expression, sexual orientation, disability, personal appearance, body size, race, ethnicity, age, religion, or nationality.

### Our Standards

**Expected behavior:**
* Use welcoming and inclusive language
* Be respectful of differing viewpoints and experiences
* Gracefully accept constructive criticism
* Focus on what is best for the community
* Show empathy towards other community members

**Unacceptable behavior:**
* Trolling, insulting/derogatory comments, and personal or political attacks
* Public or private harassment
* Publishing others' private information without explicit permission
* Other conduct which could reasonably be considered inappropriate in a professional setting

### Enforcement

Violations of the code of conduct may result in temporary or permanent ban from the project. Report violations to the project maintainers.

---

## 🚀 Getting Started

### Prerequisites

Before contributing, ensure you have:

- **Schedule I** installed (both Mono and IL2CPP versions recommended for testing)
- **MelonLoader** 0.6.5+ or 0.7.0+ installed (avoid 0.7.1)
- **.NET SDK** 6.0 or later
- **Git** for version control
- **IDE**: Visual Studio, Rider, or VS Code with C# extension

### Initial Setup

1. **Fork the Repository**
   ```bash
   # Click "Fork" on GitHub
   # Then clone your fork
   git clone https://github.com/YOUR_USERNAME/DedicatedServerMod.git
   cd DedicatedServerMod
   ```

2. **Add Upstream Remote**
   ```bash
   git remote add upstream https://github.com/ORIGINAL_OWNER/DedicatedServerMod.git
   ```

3. **Configure Build Environment**
   ```bash
   # Copy the build props template
   copy local.build.props.example local.build.props
   
   # Edit local.build.props with your game paths
   # See BUILD_SETUP.md for detailed instructions
   ```

4. **Verify Build**
   ```bash
   # Test server build
   dotnet build -c Mono_Server
   
   # Test client build
   dotnet build -c Mono_Client
   ```

5. **Read Documentation**
   - [CODING_STANDARDS.md](CODING_STANDARDS.md) - Code style and conventions
   - [BUILD_SETUP.md](BUILD_SETUP.md) - Build system details
   - [AGENTS.md](AGENTS.md) - AI assistant guidelines (if using AI tools)

---

## 🔄 Development Workflow

### Branch Strategy

We use a simplified Git workflow:

- **`main`**: Stable releases only
- **`develop`**: Active development branch (base for PRs)
- **`feature/*`**: Feature branches (your work)
- **`fix/*`**: Bug fix branches
- **`refactor/*`**: Code refactoring branches

### Creating a Feature Branch

```bash
# Update your develop branch
git checkout develop
git pull upstream develop

# Create your feature branch
git checkout -b feature/your-feature-name
```

### Making Changes

1. **Make focused commits**
   ```bash
   # Stage specific changes
   git add <files>
   
   # Commit with conventional commit format
   git commit -m "feat(Server): add player kick command"
   ```

2. **Follow commit message conventions**
   ```
   type(scope): description
   
   [optional body]
   
   [optional footer]
   ```
   
   **Types**: `feat`, `fix`, `refactor`, `docs`, `style`, `test`, `chore`, `perf`
   
   **Scopes**: `Server`, `Client`, `API`, `Shared`, `Utils`, `Docs`

3. **Keep commits atomic**
   - One logical change per commit
   - Each commit should build successfully
   - Write clear commit messages

### Testing Your Changes

Before pushing, test thoroughly:

```bash
# Build all configurations
dotnet build -c Mono_Server
dotnet build -c Mono_Client
dotnet build -c Il2cpp_Server  # if possible
dotnet build -c Il2cpp_Client  # if possible

# Verify no warnings
dotnet build -c Mono_Server -v normal

# Test runtime behavior
# 1. Start server with your changes
# 2. Connect client
# 3. Test modified functionality
# 4. Verify no regressions
```

### Staying Up to Date

```bash
# Fetch upstream changes
git fetch upstream

# Rebase your branch on latest develop
git checkout feature/your-feature-name
git rebase upstream/develop

# If conflicts occur, resolve them, then:
git add <resolved-files>
git rebase --continue
```

---

## 📝 Coding Standards

All contributions must follow [CODING_STANDARDS.md](CODING_STANDARDS.md). Key points:

### Required

- ✅ XML documentation for all public/protected APIs
- ✅ Explicit access modifiers (`public`, `private`, `internal`)
- ✅ PascalCase for classes/methods/properties
- ✅ camelCase with `_` prefix for private fields
- ✅ Sealed classes when inheritance not intended
- ✅ Proper error handling and validation
- ✅ No compiler warnings

### Prohibited

- ❌ Magic strings/numbers (use constants)
- ❌ Commented-out code
- ❌ Ignoring compiler warnings
- ❌ Mixed concerns (one class = one responsibility)
- ❌ Public exposure of internal implementation details

### Code Review Checklist

Before submitting, verify:

- [ ] Code follows naming conventions
- [ ] All public APIs have XML documentation
- [ ] No compiler warnings
- [ ] Error handling is appropriate
- [ ] Tests pass (manual testing at minimum)
- [ ] No breaking changes (or clearly documented)
- [ ] Commit messages follow conventions

---

## 📬 Pull Request Process

### Before Submitting

1. **Update your branch**
   ```bash
   git checkout develop
   git pull upstream develop
   git checkout feature/your-feature-name
   git rebase develop
   ```

2. **Squash if necessary** (optional, but preferred for cleaner history)
   ```bash
   # Interactive rebase to clean up commits
   git rebase -i upstream/develop
   ```

3. **Push to your fork**
   ```bash
   git push origin feature/your-feature-name
   # Or force push if you rebased
   git push -f origin feature/your-feature-name
   ```

### Creating the Pull Request

1. **Go to GitHub** and create a PR from your fork to `upstream/develop`

2. **Fill out the PR template** (auto-populated):
   - Clear title following conventional commits format
   - Description of changes
   - Related issues (use `Closes #123` or `Fixes #456`)
   - Testing performed
   - Screenshots/logs if applicable
   - Checklist completion

3. **PR Template Example**:
   ```markdown
   ## Description
   Adds TCP console password authentication for enhanced server security.
   
   ## Related Issues
   Closes #42
   
   ## Changes Made
   - Added password authentication to TcpConsoleServer
   - Added config options: tcpConsoleRequirePassword, tcpConsolePassword
   - Updated documentation in docs/tcp-console.md
   
   ## Testing Performed
   - [x] Tested with password enabled - authentication works
   - [x] Tested with password disabled - direct access works
   - [x] Tested incorrect password - access denied
   - [x] Tested config reload - password changes applied
   - [x] Verified Mono_Server and Il2cpp_Server builds
   
   ## Screenshots
   [Attach screenshot of TCP console authentication]
   
   ## Checklist
   - [x] Code follows CODING_STANDARDS.md
   - [x] XML documentation added for public APIs
   - [x] No compiler warnings
   - [x] Tested with both Mono and IL2CPP (or explained why not)
   - [x] No breaking changes (or documented)
   - [x] Updated relevant documentation
   ```

### Review Process

1. **Automated checks** will run (build, warnings)
2. **Maintainer review** - expect feedback and requests for changes
3. **Address feedback** by pushing new commits to your branch
4. **Approval** - maintainer will approve when ready
5. **Merge** - maintainer will merge (usually squash merge)

### After Merge

```bash
# Update your local develop
git checkout develop
git pull upstream develop

# Delete your feature branch
git branch -d feature/your-feature-name
git push origin --delete feature/your-feature-name
```

---

## 🐛 Issue Reporting

### Before Creating an Issue

1. **Search existing issues** - avoid duplicates
2. **Check documentation** - issue might be explained
3. **Verify bug** - reproduce consistently
4. **Gather information** - logs, config, steps to reproduce

### Bug Report Template

```markdown
## Bug Description
Clear and concise description of the bug.

## To Reproduce
Steps to reproduce:
1. Start server with config X
2. Connect client
3. Run command Y
4. Observe error Z

## Expected Behavior
What you expected to happen.

## Actual Behavior
What actually happened.

## Environment
- DedicatedServerMod Version: 0.2.1-beta
- MelonLoader Version: 0.6.5
- Schedule I Version: [version]
- OS: Windows 10/11 / Windows Server 2019 / Linux
- Build Type: Mono_Server / Il2cpp_Server

## Logs
```
[Paste relevant log sections from MelonLoader/Latest.log]
```

## Additional Context
Any other relevant information.
```

### Feature Request Template

```markdown
## Feature Description
Clear description of the feature you'd like to see.

## Use Case
Explain why this feature would be useful and who would benefit.

## Proposed Solution
Describe how you envision this working.

## Alternative Solutions
Other approaches you've considered.

## Additional Context
Mockups, examples from other projects, etc.
```

---

## 🛠️ Development Tips

### IDE Setup

**Visual Studio / Rider**:
- Install Harmony Analyzer (if available)
- Enable XML documentation warnings
- Configure .editorconfig (included in repo)

**VS Code**:
- Install C# extension
- Install Harmony syntax highlighting (if available)

### Common Tasks

**Add a new server command**:
1. Create class implementing `IServerCommand` in `Server/Commands/`
2. Register in `CommandManager.cs`
3. Add XML documentation
4. Test with TCP console and in-game

**Add a new config option**:
1. Add property to `ServerConfig.cs` with `[JsonProperty]`
2. Add XML documentation
3. Update example config in docs
4. Handle in relevant manager/system

**Add Harmony patch**:
1. Create patch class in appropriate folder (`Server/Game/`, `Client/Patches/`)
2. Use `[HarmonyPatch]` attributes
3. Document what you're patching and why
4. Test both Mono and IL2CPP if possible

### Debugging

**Server**:
```bash
# Enable debug logging
# In server_config.json:
{
  "debugMode": true,
  "verboseLogging": true
}

# Check logs
tail -f UserData/MelonLoader/Latest.log
```

**Client**:
```bash
# Check client logs
# Open: Schedule I/MelonLoader/Latest.log

# Enable client-side console
# Press F1 in-game (if admin)
```

### Performance Considerations

- Avoid operations in hot paths (Update loops)
- Cache frequently accessed data
- Use object pooling for network messages
- Profile before optimizing

---

## 💬 Community

### Communication Channels

- **GitHub Issues**: Bug reports, feature requests
- **GitHub Discussions**: General questions, ideas
- **Discord**: Real-time chat (link in README)
- **Pull Requests**: Code reviews, technical discussions

### Getting Help

If you're stuck:

1. Check existing documentation
2. Search closed issues/PRs
3. Ask in GitHub Discussions
4. Join Discord for real-time help

### Recognition

Contributors are recognized in:
- `CONTRIBUTORS.md` file
- Release notes for their contributions
- GitHub contributors list

---

## 📜 License

By contributing to DedicatedServerMod, you agree that your contributions will be licensed under the MIT License. See [LICENSE](LICENSE) for details.

---

## ✅ Final Checklist

Before submitting your first contribution:

- [ ] Read and understood CODING_STANDARDS.md
- [ ] Configured local build environment
- [ ] Successfully built all configurations
- [ ] Tested changes manually
- [ ] Written clear commit messages
- [ ] Added XML documentation to public APIs
- [ ] No compiler warnings
- [ ] Filled out PR template completely

---

## 🙏 Thank You!

Every contribution, no matter how small, makes DedicatedServerMod better. Whether you're fixing a typo, reporting a bug, or implementing a major feature, your help is appreciated!

**Happy coding!** 🚀
