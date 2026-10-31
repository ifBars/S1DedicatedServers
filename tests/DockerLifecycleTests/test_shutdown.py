"""Exercise the production Bash supervisor with a game-shaped stdin process."""

import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest


ROOT = Path(__file__).resolve().parents[2]
SUPERVISOR = ROOT / "Docker/server-lifecycle.sh"
FAKE_GAME = r"""
import os, signal, sys, time
mode = sys.argv[1]
signal.signal(signal.SIGTERM, lambda *_: sys.exit(91))
print('booting', flush=True)
if mode == 'crash':
    sys.exit(7)
if mode == 'early':
    time.sleep(0.3)
attempt = 0
for line in sys.stdin:
    command = line.strip()
    print('command:' + command, flush=True)
    if command != 'shutdown':
        continue
    attempt += 1
    if mode == 'fail-once' and attempt == 1:
        print('save_failed', flush=True)
        continue
    print('saving', flush=True)
    time.sleep(0.3)
    print('final_save_complete', flush=True)
    time.sleep(0.1)
    print('game_exit', flush=True)
    sys.exit(0)
sys.exit(92)  # stdin must remain open even when Docker's stdin is closed.
"""


@unittest.skipUnless(os.name == "posix", "Requires Linux/POSIX Bash and signals")
class ShutdownTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)
        self.processes = []
        self.output = self.directory / "output.log"

    def tearDown(self):
        for process in self.processes:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            process.wait(timeout=3)
            if process.stdin and not process.stdin.closed:
                process.stdin.close()
        self.temp.cleanup()

    def start(self, mode="normal", attached=False, display=False):
        env = dict(os.environ, TMPDIR=str(self.directory))
        self.display = None
        if display:
            self.display = subprocess.Popen(
                ["sleep", "30"], start_new_session=True
            )
            self.processes.append(self.display)
            env["S1DS_XVFB_PID"] = str(self.display.pid)
        with self.output.open("w") as output:
            process = subprocess.Popen(
                ["bash", str(SUPERVISOR), sys.executable, "-u", "-c", FAKE_GAME, mode],
                stdin=subprocess.PIPE if attached else subprocess.DEVNULL,
                stdout=output,
                stderr=subprocess.STDOUT,
                env=env,
                start_new_session=True,
                text=True,
            )
        self.processes.append(process)
        self.await_output("booting")
        return process

    def await_output(self, text):
        deadline = time.monotonic() + 3
        while time.monotonic() < deadline:
            contents = self.output.read_text()
            if text in contents:
                return
            time.sleep(0.01)
        self.fail(f"Missing {text!r}:\n{self.output.read_text()}")

    def assert_clean_exit(self, process, expected=0):
        self.assertEqual(process.wait(timeout=3), expected, self.output.read_text())
        self.assertEqual(list(self.directory.glob("s1ds-console.*")), [])

    def test_term_waits_for_final_save_and_keeps_display_alive(self):
        process = self.start(display=True)
        process.send_signal(signal.SIGTERM)
        self.await_output("saving")
        self.assertIsNone(process.poll())
        self.assertIsNone(self.display.poll())
        self.assert_clean_exit(process)
        self.assertEqual(self.display.wait(timeout=3), -signal.SIGTERM)
        output = self.output.read_text()
        self.assertLess(output.index("final_save_complete"), output.index("game_exit"))
        self.assertLess(output.index("game_exit"), output.index("Game server exited"))

    def test_repeated_term_and_int_request_only_one_shutdown(self):
        process = self.start()
        process.send_signal(signal.SIGINT)
        self.await_output("saving")
        process.send_signal(signal.SIGTERM)
        time.sleep(0.02)
        process.send_signal(signal.SIGINT)
        self.assert_clean_exit(process)
        self.assertEqual(self.output.read_text().count("command:shutdown"), 1)

    def test_stop_before_console_reader_is_ready_is_queued(self):
        process = self.start(mode="early")
        process.send_signal(signal.SIGTERM)
        self.assert_clean_exit(process)
        self.assertIn("final_save_complete", self.output.read_text())

    def test_attached_input_and_eof_do_not_break_later_stop(self):
        process = self.start(attached=True)
        process.stdin.write("save\n")
        process.stdin.flush()
        self.await_output("command:save")
        process.stdin.close()
        process.send_signal(signal.SIGTERM)
        self.assert_clean_exit(process)

    def test_failed_save_keeps_game_running_and_allows_manual_retry(self):
        process = self.start(mode="fail-once", attached=True)
        process.send_signal(signal.SIGTERM)
        self.await_output("save_failed")
        process.send_signal(signal.SIGTERM)
        time.sleep(0.1)
        self.assertIsNone(process.poll())
        self.assertEqual(self.output.read_text().count("command:shutdown"), 1)
        process.stdin.write("shutdown\n")
        process.stdin.flush()
        self.assert_clean_exit(process)

    def test_natural_game_failure_preserves_exit_code(self):
        process = self.start(mode="crash")
        self.assert_clean_exit(process, expected=7)


if __name__ == "__main__":
    unittest.main()
