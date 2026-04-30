from __future__ import annotations

import contextlib
import argparse
import html
import io
import json
import socket
import threading
import traceback
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse

import m


DEFAULT_HOST = '127.0.0.1'
DEFAULT_PORT = 8765


INDEX_HTML = r'''<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Codex 会话同步工具</title>
  <style>
    :root {
      color-scheme: light;
      font-family: "Microsoft YaHei", "Segoe UI", Arial, sans-serif;
      background: #f4f6f8;
      color: #1d2939;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      background: linear-gradient(180deg, #eef4ff 0%, #f7f8fa 40%, #f4f6f8 100%);
    }
    main {
      width: min(1080px, calc(100vw - 32px));
      margin: 0 auto;
      padding: 28px 0 36px;
    }
    header {
      display: flex;
      justify-content: space-between;
      gap: 16px;
      align-items: flex-end;
      margin-bottom: 18px;
    }
    h1 {
      margin: 0 0 8px;
      font-size: 28px;
      line-height: 1.2;
    }
    p {
      margin: 0;
      color: #667085;
      line-height: 1.65;
    }
    .status {
      min-width: 150px;
      border: 1px solid #d0d5dd;
      border-radius: 8px;
      padding: 10px 12px;
      background: rgba(255, 255, 255, 0.8);
      font-size: 13px;
      color: #475467;
    }
    section {
      background: rgba(255, 255, 255, 0.92);
      border: 1px solid #d9e0e8;
      border-radius: 8px;
      padding: 18px;
      box-shadow: 0 8px 28px rgba(31, 44, 71, 0.08);
      margin-bottom: 16px;
    }
    .grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }
    label {
      display: block;
      font-size: 13px;
      font-weight: 700;
      color: #344054;
      margin-bottom: 7px;
    }
    input[type="text"], select {
      width: 100%;
      height: 40px;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      padding: 0 11px;
      background: #fff;
      color: #1d2939;
      font-size: 14px;
    }
    input[type="text"]:focus, select:focus {
      outline: 2px solid #84caff;
      border-color: #2e90fa;
    }
    .mode-options {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 10px;
    }
    .mode-card {
      border: 1px solid #d0d5dd;
      border-radius: 8px;
      padding: 12px;
      cursor: pointer;
      background: #fff;
    }
    .mode-card input { margin-right: 8px; }
    .mode-card strong {
      display: inline-block;
      margin-bottom: 6px;
      color: #1d2939;
    }
    .mode-card span {
      display: block;
      color: #667085;
      font-size: 13px;
      line-height: 1.45;
    }
    .actions {
      display: flex;
      align-items: center;
      gap: 10px;
      flex-wrap: wrap;
      margin-top: 16px;
    }
    button {
      height: 40px;
      border: 0;
      border-radius: 6px;
      padding: 0 16px;
      font-weight: 700;
      cursor: pointer;
      color: white;
      background: #175cd3;
    }
    button.secondary { background: #344054; }
    button.danger { background: #b42318; }
    button:disabled {
      opacity: 0.55;
      cursor: wait;
    }
    .apply-check {
      display: inline-flex;
      align-items: center;
      gap: 7px;
      font-size: 13px;
      color: #475467;
      margin-left: auto;
    }
    pre {
      min-height: 260px;
      max-height: 520px;
      overflow: auto;
      white-space: pre-wrap;
      word-break: break-word;
      margin: 0;
      border-radius: 8px;
      background: #101828;
      color: #e4e7ec;
      padding: 16px;
      font: 13px/1.55 Consolas, "Cascadia Mono", monospace;
    }
    .hint {
      margin-top: 8px;
      font-size: 12px;
      color: #667085;
    }
    @media (max-width: 780px) {
      header, .grid, .mode-options { grid-template-columns: 1fr; display: grid; }
      header { align-items: stretch; }
      .apply-check { margin-left: 0; }
    }
  </style>
</head>
<body>
  <main>
    <header>
      <div>
        <h1>Codex 会话同步工具</h1>
        <p>本工具只访问本机 Codex 数据目录。默认预览，不会写入；勾选确认并点击执行写入后才会修改文件和 SQLite。</p>
      </div>
      <div class="status" id="status">准备就绪</div>
    </header>

    <section>
      <div class="grid">
        <div>
          <label for="codexHome">Codex Home</label>
          <input id="codexHome" type="text" />
          <div class="hint">默认读取 CODEX_HOME 或当前用户的 .codex。</div>
        </div>
        <div>
          <label for="backupDir">备份目录</label>
          <input id="backupDir" type="text" placeholder="可选，写入前备份 SQLite 文件" />
          <div class="hint">执行写入时建议填写，例如桌面上的 codex-backup。</div>
        </div>
      </div>
    </section>

    <section>
      <label>同步模式</label>
      <div class="mode-options">
        <label class="mode-card">
          <input type="radio" name="mode" value="mutual" checked />
          <strong>全供应商互同步</strong>
          <span>配置中的每个 provider 最终看到同一批会话，冲突跳过并报告。</span>
        </label>
        <label class="mode-card">
          <input type="radio" name="mode" value="openai" />
          <strong>OpenAI 同步到全部</strong>
          <span>以 source provider 为源，给其它 provider 创建镜像。</span>
        </label>
        <label class="mode-card">
          <input type="radio" name="mode" value="migrate" />
          <strong>单目标迁移</strong>
          <span>把非保留 provider 的历史会话迁移到目标 provider。</span>
        </label>
      </div>

      <div class="grid" style="margin-top:16px">
        <div>
          <label for="sourceProvider">源 provider</label>
          <input id="sourceProvider" type="text" value="openai" />
        </div>
        <div>
          <label for="targetProvider">目标 provider</label>
          <input id="targetProvider" type="text" value="openai" />
        </div>
      </div>

      <div class="actions">
        <button id="previewBtn">预览</button>
        <button id="applyBtn" class="danger">执行写入</button>
        <button id="defaultsBtn" class="secondary">恢复默认路径</button>
        <label class="apply-check">
          <input id="confirmApply" type="checkbox" />
          我已备份或确认可以写入
        </label>
      </div>
    </section>

    <section>
      <label>运行输出</label>
      <pre id="output">等待运行。</pre>
    </section>
  </main>

  <script>
    const statusEl = document.getElementById('status');
    const outputEl = document.getElementById('output');
    const codexHomeEl = document.getElementById('codexHome');
    const backupDirEl = document.getElementById('backupDir');
    const sourceProviderEl = document.getElementById('sourceProvider');
    const targetProviderEl = document.getElementById('targetProvider');
    const confirmApplyEl = document.getElementById('confirmApply');
    const buttons = [document.getElementById('previewBtn'), document.getElementById('applyBtn'), document.getElementById('defaultsBtn')];

    function selectedMode() {
      return document.querySelector('input[name="mode"]:checked').value;
    }

    function setBusy(isBusy) {
      buttons.forEach((button) => { button.disabled = isBusy; });
      statusEl.textContent = isBusy ? '运行中' : '准备就绪';
    }

    async function loadDefaults() {
      const response = await fetch('/api/defaults');
      const data = await response.json();
      codexHomeEl.value = data.codex_home;
      backupDirEl.value = data.backup_dir;
      sourceProviderEl.value = 'openai';
      targetProviderEl.value = 'openai';
      outputEl.textContent = data.message;
    }

    async function run(apply) {
      if (apply && !confirmApplyEl.checked) {
        outputEl.textContent = '执行写入前，请先勾选确认。';
        return;
      }
      setBusy(true);
      outputEl.textContent = '正在运行...';
      try {
        const response = await fetch('/api/run', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            mode: selectedMode(),
            apply,
            codex_home: codexHomeEl.value,
            backup_dir: backupDirEl.value,
            source_provider: sourceProviderEl.value,
            target_provider: targetProviderEl.value
          })
        });
        const data = await response.json();
        statusEl.textContent = data.ok ? '完成' : '失败';
        outputEl.textContent = data.output || data.error || '无输出';
      } catch (error) {
        statusEl.textContent = '失败';
        outputEl.textContent = String(error);
      } finally {
        buttons.forEach((button) => { button.disabled = false; });
      }
    }

    document.getElementById('previewBtn').addEventListener('click', () => run(false));
    document.getElementById('applyBtn').addEventListener('click', () => run(true));
    document.getElementById('defaultsBtn').addEventListener('click', loadDefaults);
    loadDefaults();
  </script>
</body>
</html>
'''


class SyncRequestHandler(BaseHTTPRequestHandler):
    server_version = 'CodexSessionSync/1.0'

    def log_message(self, format: str, *args: object) -> None:
        return

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path == '/':
            self.send_html(INDEX_HTML)
            return
        if parsed.path == '/api/defaults':
            default_home = str(m.default_codex_home().expanduser())
            default_backup = str(Path.home() / 'Desktop' / 'codex-session-sync-backup')
            self.send_json(
                {
                    'codex_home': default_home,
                    'backup_dir': default_backup,
                    'message': '默认路径已加载。建议先预览，确认无冲突后再执行写入。',
                }
            )
            return
        self.send_error(404)

    def do_POST(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path != '/api/run':
            self.send_error(404)
            return
        try:
            payload = self.read_json_body()
            output = run_sync(payload)
            self.send_json({'ok': True, 'output': output})
        except Exception:
            self.send_json({'ok': False, 'error': traceback.format_exc()}, status=500)

    def read_json_body(self) -> dict[str, object]:
        length = int(self.headers.get('Content-Length', '0') or '0')
        raw_body = self.rfile.read(length).decode('utf-8')
        data = json.loads(raw_body or '{}')
        if not isinstance(data, dict):
            raise ValueError('Request body must be a JSON object.')
        return data

    def send_html(self, content: str) -> None:
        body = content.encode('utf-8')
        self.send_response(200)
        self.send_header('Content-Type', 'text/html; charset=utf-8')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def send_json(self, payload: dict[str, object], status: int = 200) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def normalize_text(value: object) -> str:
    return str(value or '').strip()


def run_sync(payload: dict[str, object]) -> str:
    mode = normalize_text(payload.get('mode')) or 'mutual'
    codex_home = normalize_text(payload.get('codex_home'))
    backup_dir = normalize_text(payload.get('backup_dir'))
    source_provider = normalize_text(payload.get('source_provider')) or 'openai'
    target_provider = normalize_text(payload.get('target_provider')) or 'openai'
    apply = bool(payload.get('apply'))

    args = ['--codex-home', codex_home or str(m.default_codex_home()), '--target-provider', target_provider]
    if backup_dir:
        args.extend(['--backup-dir', backup_dir])
    if apply:
        args.append('--apply')

    if mode == 'mutual':
        args.append('--sync-all-providers-mutually')
    elif mode == 'openai':
        args.extend(['--sync-openai-to-all-providers', '--source-provider', source_provider])
    elif mode == 'migrate':
        pass
    else:
        raise ValueError(f'Unsupported mode: {html.escape(mode)}')

    stream = io.StringIO()
    with contextlib.redirect_stdout(stream), contextlib.redirect_stderr(stream):
        exit_code = m.main(args)
    output = stream.getvalue()
    if exit_code:
        output += f'\nExit code: {exit_code}\n'
    return output


def find_available_port(host: str, start_port: int) -> int:
    for port in range(start_port, start_port + 100):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            try:
                sock.bind((host, port))
            except OSError:
                continue
            return port
    raise OSError('No available local port found.')


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description='Local Web UI for Codex session synchronization.')
    parser.add_argument('--host', default=DEFAULT_HOST)
    parser.add_argument('--port', type=int, default=DEFAULT_PORT)
    parser.add_argument('--no-browser', action='store_true', help='Start the server without opening a browser.')
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    port = find_available_port(args.host, args.port)
    server = ThreadingHTTPServer((args.host, port), SyncRequestHandler)
    url = f'http://{args.host}:{port}/'
    print(f'Codex session sync Web UI: {url}')
    print('Close this window to stop the tool.')
    if not args.no_browser:
        threading.Timer(0.5, lambda: webbrowser.open(url)).start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
