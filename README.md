# ScreenshotSorter

Snipping Tool（Win + Shift + S / PrintScreen）で保存されたスクリーンショットを、
**撮影日（ファイルの作成日時）ごとのフォルダ**へ自動で移動する、タスクトレイ常駐の Windows アプリです。

```
スクリーンショット\
├─ 2025-01-30\
│   └─ スクリーンショット 2025-01-30 101112.png
└─ 2025-01-31\
    ├─ スクリーンショット 2025-01-31 090000.png
    └─ スクリーンショット 2025-01-31 090000 (2).png   ← 同名があれば連番
```

## 主な機能

- 監視フォルダの既定は Windows の「スクリーンショット」フォルダ（Known Folder API で取得するので OneDrive へのリダイレクトにも対応）
- 監視フォルダ直下に増えた `.png` / `.jpg` / `.jpeg` を、同じフォルダ内の `yyyy-MM-dd` フォルダへ移動
- サブフォルダは監視しない（移動済みのファイルを再び拾わない）
- 書き込み中でロックされていたら少し待ってリトライ（最大 6 回、合計約 15 秒）
- 移動先に同名ファイルがあれば上書きせず `名前 (2).png` のように連番を付ける
- タスクトレイのメニュー：監視フォルダを開く／一時停止・再開／設定／ログフォルダを開く／終了
- 設定：監視フォルダの変更、起動時に既存の画像もまとめて整理（既定 OFF）、Windows 起動時に自動で起動
- 移動のたびに通知は出しません（ログには残ります）

> 一時停止中に保存された画像は、再開しても自動では移動しません。
> 「起動時に既存の画像も整理する」を ON にしておけば、次回起動時にまとめて整理されます。

## exe の落とし方

1. このリポジトリの **Releases**（右側の「Releases」、または https://github.com/ferrarissound-design/ScreenshotSorter/releases）を開く
2. 最新バージョンの **Assets** にある `ScreenshotSorter.exe` をダウンロード
3. 好きな場所（例：`C:\Users\<あなた>\Apps\ScreenshotSorter\`）に置いてダブルクリック
   - .NET ランタイムのインストールは不要です（exe 単体で動きます）
   - 起動するとタスクトレイ（画面右下の「^」の中）にアイコンが出ます
4. 自動起動したい場合は、トレイアイコンを右クリック →「設定...」→「Windows 起動時に自動で起動する」にチェック

> **exe の置き場所について**：自動起動は exe のフルパスを登録します。
> 「ダウンロード」フォルダに置いたまま後で消すと自動起動できなくなるので、決まった場所に置くのがおすすめです。
> 新しいバージョンに更新するときは、アプリを「終了」してから同じ場所の exe を上書きしてください。

開発途中のビルド（main の最新）は **Actions** → 該当の実行 → 下部の **Artifacts** から zip で取得できます（GitHub へのログインが必要）。

## SmartScreen の警告が出たとき

この exe にはコード署名をしていないため、初回起動時に
「Windows によって PC が保護されました」という青い画面が出ることがあります。

1. 「**詳細情報**」をクリック
2. 「**実行**」ボタンを押す

それでも起動できない場合は、exe を右クリック →「プロパティ」→ 下部の「**許可する**」（セキュリティ: このファイルは他のコンピューターから取得したものです…）にチェック →「OK」してから再度実行してください。

> 心配な場合は、Releases の exe がこのリポジトリの GitHub Actions でビルドされたものであることを、
> Release ページと Actions の実行履歴（同じタグ）で確認できます。

## ログと設定の保存場所

どちらも `%AppData%\ScreenshotSorter\`（通常は `C:\Users\<あなた>\AppData\Roaming\ScreenshotSorter\`）です。
エクスプローラーのアドレスバーに `%AppData%\ScreenshotSorter` と入力すると開けます。
トレイメニューの「ログフォルダを開く」からも開けます。

| ファイル | 内容 |
| --- | --- |
| `log.txt` | 起動・終了、移動したファイル（`元のパス → 移動先`）、エラー（日時・内容・スタックトレース）。1MB を超えると `log.old.txt` に切り替え |
| `settings.json` | 設定（監視フォルダ、起動時に整理、自動起動） |

予期しないエラーで終了した場合は、ログの場所がダイアログに表示されます。
アプリをやめるときは、トレイから「終了」→（自動起動 ON なら先に設定で OFF にする）→ exe と上記フォルダを削除すれば完全に消えます。

## 新しいバージョンを出す手順（タグの付け方）

GitHub Actions がタグをトリガーにビルドし、Releases を作って exe を添付します。
**タグ名 `v1.2.3` の `1.2.3` がそのままアプリのバージョン表示になります**（`vX.Y.Z` 形式以外はエラーで止まります）。

### GitHub の Web 画面だけでやる場合

1. 変更を main に入れる（PR をマージ）し、Actions の main のビルドが緑になったのを確認
2. リポジトリの **Releases** →「**Draft a new release**」
3. 「Choose a tag」に `v1.2.3` と入力 →「**Create new tag: v1.2.3 on publish**」、Target は `main`
4. 「**Publish release**」を押す
5. タグが作られたことで Actions が動き、数分後にその Release に `ScreenshotSorter.exe` が添付されます
   （Release を先に作った場合は、Actions がそこへ exe を追加する作りになっています）

### git コマンドでやる場合

```bash
git checkout main
git pull
git tag v1.2.3
git push origin v1.2.3
```

数分後、**Actions** の `build` ワークフローが完了すると **Releases** に `ScreenshotSorter v1.2.3` が作られ、`ScreenshotSorter.exe` が添付されます。

### バージョン番号の付け方の目安

- 不具合修正だけ：`v1.0.0` → `v1.0.1`
- 機能追加：`v1.0.1` → `v1.1.0`
- 大きな変更：`v1.1.0` → `v2.0.0`

### 失敗したとき

- Actions が赤くなった場合は、その実行のログを確認してください（テスト失敗ならタグを付け直す前に修正が必要です）。
- タグを付け間違えた場合：`git push origin :refs/tags/v1.2.3` でリモートのタグを消し、Releases 画面で該当 Release も削除してから付け直します。

## 開発

- C# / WPF / .NET 10（LTS）、`win-x64` self-contained の単一 exe
- 構成
  - `src/ScreenshotSorter.Core` … UI に依存しないロジック（移動ルール、移動処理、監視、設定、ログ）
  - `src/ScreenshotSorter` … WPF アプリ（トレイ、設定画面、Known Folder、自動起動）
  - `tests/ScreenshotSorter.Tests` … xUnit の単体テスト
- CI：`.github/workflows/build.yml`（windows-latest）
  - main への push / main 向け PR：テスト → publish → Artifacts にアップロード
  - `v*.*.*` タグの push：上記 ＋ GitHub Release 作成・exe 添付

ローカルでビルドする場合（Windows、.NET 10 SDK）：

```bash
dotnet test tests/ScreenshotSorter.Tests
dotnet publish src/ScreenshotSorter -c Release -p:Version=1.2.3 -o publish
```
