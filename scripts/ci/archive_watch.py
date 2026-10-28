"""Report archive changes without executing upstream code or claiming compatibility."""

import json
import os
from pathlib import Path
import re
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]


def run(*args, cwd=None, data=None):
    return subprocess.run(args, cwd=cwd, input=data, text=True, encoding='utf-8', check=True,
                          capture_output=True, timeout=120).stdout.strip()


def marker(repository, branch, revision):
    return f"<!-- s1ds-archive-watch:{repository}:{branch}:{revision} -->"


def investigation(config, branch, revision, parent, subject, changed):
    affected = sorted(set(changed) & config['watched_paths'].keys())
    version = re.fullmatch(r'Auto-update for version ([0-9A-Za-z. -]+)', subject)
    if not affected and not version:
        return None
    repository = config['repository']
    revision_url = f'https://github.com/{repository}/commit/{revision}'
    compare_url = f'https://github.com/{repository}/compare/{parent}...{revision}'
    name = version.group(1) if version else revision[:12]
    lines = [marker(repository, branch, revision),
             f'Archive branch: `{branch}`. Revision: [{revision[:12]}]({revision_url}).',
             f'[Compare this update]({compare_url}).', '',
             'This is an investigation trigger, not a confirmed bug or compatibility result.',
             'The archive contains stripped Mono declarations. Unchanged declarations do not '
             'prove unchanged method bodies, assets, or IL2CPP behavior.', '',
             '### Watched changes']
    lines += [f'- `{path}`: {config["watched_paths"][path]}' for path in affected]
    if not affected:
        lines.append('- No watched declarations changed. A new game version still needs compatibility review.')
    lines += ['', '### Investigation checklist',
              '- [ ] Inspect the upstream diff and affected S1DS patches; record applicable changes or a no-change rationale.',
              '- [ ] Refresh the appropriate private game assembly branch and record the tested game version and assembly revisions.',
              '- [ ] Run the shared regression suite and all four build configurations for the proposed mod commit; link the passing run.',
              '- [ ] Record live-game validation as performed or explicitly not performed. CI does not run the game.',
              '', 'For console-related changes, optional local checks should exercise the actual input action '
              'with a custom `console.open` group on both runtimes: open, close, reopen, pause, revoke access, '
              'denied reopening, reconnect, and server command denial.',
              '', 'Do not infer a regression from this notification or weaken server authorization to resolve it.']
    return {'title': f'Investigate game update: {branch} / {name}', 'body': '\n'.join(lines)}


def existing_markers(pages):
    result = set()
    for page in pages:
        for issue in page:
            if 'pull_request' not in issue:
                result.update(re.findall(r'<!-- s1ds-archive-watch:[^\r\n]+? -->', issue.get('body') or ''))
    return result


def inspect_branch(config, branch, baseline, directory):
    run('git', 'init', '--bare', directory)
    run('git', 'fetch', '--filter=blob:none', '--no-tags',
        f'https://github.com/{config["repository"]}.git',
        f'refs/heads/{branch}:refs/heads/watched', cwd=directory)
    # A rewritten branch must be investigated instead of silently resetting its baseline.
    run('git', 'merge-base', '--is-ancestor', baseline, 'watched', cwd=directory)
    revisions = run('git', 'rev-list', '--reverse', '--topo-order',
                    f'{baseline}..watched', cwd=directory).splitlines()
    for revision in revisions:
        parent = run('git', 'rev-parse', f'{revision}^', cwd=directory)
        subject = run('git', 'show', '-s', '--format=%s', revision, cwd=directory)
        changed = run('git', 'diff', '--name-only', '--no-renames', parent, revision,
                      cwd=directory).splitlines()
        item = investigation(config, branch, revision, parent, subject, changed)
        if item:
            yield marker(config['repository'], branch, revision), item


def publish(items, seen, repository, dry_run, api=run):
    count = 0
    for key, item in items:
        if key in seen:
            continue
        print(json.dumps(item))
        if not dry_run:
            api('gh', 'api', f'repos/{repository}/issues', '--method', 'POST',
                '--input', '-', data=json.dumps(item))
        seen.add(key)
        count += 1
    return count


def main():
    config = json.loads((ROOT / '.github/archive-watch.json').read_text())
    repository = os.environ['GH_REPO']
    dry_run = os.environ.get('WATCH_DRY_RUN', 'true') == 'true'
    pages = json.loads(run('gh', 'api', '--paginate', '--slurp',
                           f'repos/{repository}/issues?state=all&per_page=100'))
    seen = existing_markers(pages)
    count = 0
    with tempfile.TemporaryDirectory(prefix='s1ds-archive-') as directory:
        for index, (branch, baseline) in enumerate(config['branches'].items()):
            items = inspect_branch(config, branch, baseline, str(Path(directory) / str(index)))
            count += publish(items, seen, repository, dry_run)
    summary = f'Archive watcher: {count} new investigation(s); dry run: {dry_run}. No live-game tests performed.\n'
    print(summary)
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as stream:
            stream.write(summary)


if __name__ == '__main__':
    main()
