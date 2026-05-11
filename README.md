# NewLauncher

## 日本語

NewLauncher は、NewEngine と NewEditor の導入、更新、プロジェクト起動をまとめて扱う Windows 向けランチャーです。エンジンのバージョン管理と Launcher 自身の更新を自動化し、制作を始めるまでの手間を減らします。

### 主な機能

- NewEngine の最新版チェック
- 過去バージョンを含むリリース一覧の取得
- 任意バージョンのインストール
- インストール済みバージョンの表示と重複インストール防止
- ダウンロード中のプログレスバーとパーセント表示
- SHA256 によるパッケージ検証
- NewLauncher 起動時の自動アップデート
- 起動時の NewEngine 最新版チェックと、更新がある場合の分かりやすい通知
- 新規プロジェクト作成
- 既存プロジェクトの登録
- 使用する Engine バージョンを選んで NewEditor を起動
- プロジェクトフォルダ、Engine フォルダの素早いオープン
- 日本語 / English 表示切り替え
- ライト / ダーク / システムテーマ切り替え

### 基本フロー

1. `リリース確認` で利用可能な Engine バージョンを取得します。
2. バージョンを選択します。
3. 未インストールの場合は `インストール` を押します。
4. プロジェクトを作成または追加します。
5. Engine と Project を選び、`プロジェクトを起動` で NewEditor を開きます。

### 自動アップデート

NewLauncher は起動時に GitHub Releases を確認し、より新しい NewLauncher がある場合は自動的に取得します。更新が完了すると Launcher を再起動し、新しいバージョンで続行します。

## English

NewLauncher is the Windows launcher for installing and updating NewEngine / NewEditor and opening projects. It handles engine version management and launcher self-updates so users can move from install to creation quickly.

### Features

- Check the latest NewEngine release
- Load the full release list, including older versions
- Install a selected engine version
- Show installed versions and prevent duplicate installs
- Display download progress with a progress bar and percentage
- Verify packages with SHA256
- Auto-update NewLauncher on startup
- Check for newer NewEngine releases on startup and show a clear notice when one is available
- Create new projects
- Register existing projects
- Launch NewEditor with the selected Engine version
- Open project and Engine folders quickly
- Switch between Japanese and English UI
- Switch between light, dark, and system color themes

### Basic Flow

1. Press `Check Releases` to load available Engine versions.
2. Select the version you want.
3. Press `Install` if it is not already installed.
4. Create or add a project.
5. Select an Engine and Project, then press `Launch Project` to open NewEditor.

### Auto Update

NewLauncher checks GitHub Releases on startup. If a newer NewLauncher version is available, it downloads the update, replaces the local launcher files, restarts, and continues with the updated version.
