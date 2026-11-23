# 如期（Ruqi）

**如期**是一款基于 `.NET 10 MAUI` + `SQLite` 构建的纯本地离线、免登录人民币订阅管理与续费提醒账本，支持 **Android / Windows / macOS / iOS** 全平台。

- **纯本地离线**：零云端服务器、无需登录，数据仅保存在本机 SQLite 数据库中，支持通过系统文件选择器导出与原子恢复 JSON 备份。
- **核心功能**：离线预设应用目录、跨应用全局常用账号池、付款渠道管理、月末/闰年锚定周期引擎、漏开多期自动补齐与幂等扣款确认、手工补录账单、一次性到期顺手记账、月度/年度支出统计与本地定时提醒。

---

## 工程结构

- `src/Ruqi.Core`：纯 C# 领域模型、周期算法、SQLite 数据层、统计与事务性 JSON 备份恢复引擎。
- `src/Ruqi.App`：`.NET 10 MAUI` 全平台客户端（Android / Windows / macOS / iOS）。
- `tests/Ruqi.Core.Tests`：核心领域与 SQLite 集成测试。
- `.github/workflows/ci.yml`：`main` 分支 push / PR 自动运行单元测试。
- `.github/workflows/release.yml`：全平台自动递增版本号、打 Tag、并行打包并发布 GitHub Release。

---

## GitHub 全自动发版指南

### 1. 关联并推送到 GitHub

在 GitHub 新建一个空仓库后，在项目根目录执行：

```bash
git remote add origin https://github.com/<你的用户名>/<你的仓库名>.git
git push -u origin main
```

### 2. 触发自动递增版本号与全平台 Release（两种方式）

- **方式一（推荐 · 一键按钮）**：打开 GitHub 仓库 → **Actions** → **全平台自动构建与发版 (Release)** → 点击 **Run workflow**：
  - 选择 `bump`（`patch` 递增修订号如 `1.0.0 → 1.0.1`、`minor` 递增次版本号、`major` 递增主版本号），首次无 Tag 时默认发布 `v1.0.0`；
  - 也可直接填写 `custom_version` 指定具体版本号。
  - 流水线将自动打 Tag、跑单元测试、并行编译 4 平台安装包、生成 `SHA256SUMS.txt` 与更新日志，并发布到 **Releases**。
- **方式二（推 Tag 触发）**：
  ```bash
  git tag v1.0.0
  git push origin v1.0.0
  ```

### 3. 可选签名 Secrets 配置（不配也能直接跑通）

进入 GitHub 仓库 → **Settings → Secrets and variables → Actions** 按需添加：

#### Android 固定正式签名（推荐配置，便于后续版本无缝覆盖升级）
1. 本地生成一次正式签名密钥库（妥善保管，切勿提交到仓库）：
   ```powershell
   keytool -genkey -v -keystore ruqi-release.keystore -alias ruqi -keyalg RSA -keysize 2048 -validity 10000
   ```
2. 转为 Base64：
   ```powershell
   [Convert]::ToBase64String([IO.File]::ReadAllBytes("ruqi-release.keystore")) | Set-Clipboard
   ```
3. 在 GitHub Secrets 中配置以下 4 项：
   - `ANDROID_KEYSTORE_BASE64`：上一步复制的 Base64 字符串
   - `ANDROID_KEYSTORE_PASSWORD`：密钥库密码
   - `ANDROID_KEY_ALIAS`：`ruqi`
   - `ANDROID_KEY_PASSWORD`：密钥密码

> 未配置上述 Secrets 时，流水线会自动使用默认开发签名打包 APK，保证零配置跑通。

#### iOS 正式签名（可选）
- 未配置时，流水线会自动编译并封装 `Ruqi-vX.Y.Z-ios-arm64-unsigned.ipa`（可用于 AltStore / Sideloadly / 巨魔等工具安装）。
- 拥有 Apple Developer 证书后，在 GitHub Secrets 中配置以下 3 项即可自动切换为正式签名 `.ipa`：
  - `IOS_P12_BASE64`
  - `IOS_P12_PASSWORD`
  - `IOS_PROVISION_PROFILE_BASE64`

---

## 本地开发与构建命令

```powershell
# 运行核心单元测试
dotnet test tests/Ruqi.Core.Tests/Ruqi.Core.Tests.csproj

# 打包 Android arm64 Release APK
dotnet publish src/Ruqi.App/Ruqi.App.csproj -f net10.0-android -c Release -r android-arm64 -p:AndroidPackageFormat=apk

# 打包 Windows x64 免安装绿色便携版
dotnet publish src/Ruqi.App/Ruqi.App.csproj -f net10.0-windows10.0.19041.0 -c Release -r win-x64 -p:SelfContained=true -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true
```
