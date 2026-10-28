import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('archive_watch', ROOT / 'scripts/ci/archive_watch.py')
watch = importlib.util.module_from_spec(spec)
spec.loader.exec_module(watch)


class ArchiveWatcherTests(unittest.TestCase):
    def setUp(self):
        output = patch('builtins.print')
        output.start()
        self.addCleanup(output.stop)
        self.config = json.loads((ROOT / '.github/archive-watch.json').read_text())
        self.path = 'ScheduleOne-stripped/UI/ConsoleUI.cs'

    def item(self, subject='Auto-update for version 0.4.8f1 Alternate', paths=()):
        return watch.investigation(self.config, 'alternate', 'a' * 40, 'b' * 40, subject, paths)

    def test_new_version_without_declaration_change_still_triggers(self):
        self.assertIn('No watched declarations changed', self.item()['body'])

    def test_watched_addition_or_deletion_triggers_without_version(self):
        self.assertIn('Console opening', self.item('Update declarations', [self.path])['body'])

    def test_unrelated_non_version_commit_is_ignored(self):
        self.assertIsNone(self.item('Update readme', ['README.md']))

    def test_untrusted_subject_is_not_copied_into_issue(self):
        item = self.item('@everyone execute this', [self.path])
        self.assertNotIn('@everyone', json.dumps(item))

    def test_branches_have_independent_markers(self):
        self.assertNotEqual(watch.marker('r', 'alternate', 'a'), watch.marker('r', 'alternate-beta', 'a'))

    def test_pagination_includes_closed_issues_but_not_prs(self):
        key = watch.marker('r', 'alternate', 'a')
        pages = [[{'body': None}], [{'body': key, 'state': 'closed'},
                                  {'body': 'ignored', 'pull_request': {}}]]
        self.assertEqual({key}, watch.existing_markers(pages))

    def test_repeat_run_and_duplicate_item_do_not_publish(self):
        key = watch.marker('r', 'alternate', 'a')
        calls = []
        api = lambda *args, **kwargs: calls.append((args, kwargs))
        seen = set()
        items = [(key, self.item()), (key, self.item())]
        self.assertEqual(1, watch.publish(items, seen, 'target', False, api))
        self.assertEqual(0, watch.publish(items, seen, 'target', False, api))
        self.assertEqual(1, len(calls))

    def test_dry_run_does_not_publish(self):
        def fail(*args, **kwargs):
            self.fail('dry run called write API')
        self.assertEqual(1, watch.publish([('key', self.item())], set(), 'target', True, fail))

    def test_failed_publication_is_not_recorded(self):
        def fail(*args, **kwargs):
            raise RuntimeError('API unavailable')
        seen = set()
        with self.assertRaises(RuntimeError):
            watch.publish([('key', self.item())], seen, 'target', False, fail)
        self.assertEqual(set(), seen)

    def test_missing_or_rewritten_baseline_fails_before_publication(self):
        def git(*args, **kwargs):
            if 'merge-base' in args:
                raise RuntimeError('baseline is not an ancestor')
            return ''
        with patch.object(watch, 'run', side_effect=git):
            with self.assertRaises(RuntimeError):
                list(watch.inspect_branch(self.config, 'alternate', 'baseline', 'unused'))

    def test_missed_versions_are_processed_in_order_and_deletions_are_watched(self):
        def git(*args, **kwargs):
            if 'rev-list' in args:
                return 'first\nsecond'
            if 'rev-parse' in args:
                return 'parent'
            if 'show' in args:
                return 'Auto-update for version 0.4.8f1 Alternate'
            if 'diff' in args:
                self.assertIn('--no-renames', args)
                return self.path
            return ''
        with patch.object(watch, 'run', side_effect=git):
            items = list(watch.inspect_branch(self.config, 'alternate', 'baseline', 'unused'))
        self.assertEqual(2, len(items))
        self.assertIn(':first -->', items[0][0])
        self.assertIn(':second -->', items[1][0])
        self.assertIn(self.path, items[0][1]['body'])

    def test_release_and_pr_build_share_mandatory_regression_gate(self):
        build = (ROOT / '.github/workflows/build.yml').read_text()
        release = (ROOT / '.github/workflows/auto-release.yml').read_text()
        self.assertIn('uses: ./.github/workflows/regression-tests.yml', build)
        self.assertIn('uses: ./.github/workflows/regression-tests.yml', release)
        self.assertIn('build:\n    needs: regression', build)
        self.assertIn('create-release:\n    needs: [check-version, regression]', release)
        self.assertIn('      - create-release', release)
        self.assertNotIn('continue-on-error', (ROOT / '.github/workflows/regression-tests.yml').read_text())


if __name__ == '__main__':
    unittest.main()
