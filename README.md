
# Unity ADB Tools

Unity ADB Tools 是一个简便易用的Unity编辑器扩展工具，用于管理Android设备连接、应用调试和打包。它提供了直观的用户界面，让开发者无需离开Unity编辑器即可完成ADB设备连接和应用打包部署工作。

## 功能特点

- ADB设备连接管理
  - 自动检测设备IP地址
  - 快速连接Android设备
  - 支持无线调试
  - 实时显示设备列表
- Android应用构建与部署
  - 基于模板的打包系统
  - 支持多个应用配置文件
  - 批量场景打包选择
  - 一键打包并安装
- 便捷调试功能
  - 实时检测设备状态
  - 安装APK文件
  - 自动版本管理
  - 操作日志记录

## 安装方法

### 通过Unity Package Manager安装

1. 打开Unity项目
2. 打开Window > Package Manager
3. 点击左上角的"+"按钮
4. 选择"Add package from git URL..."
5. 输入以下URL:
   ```
   https://github.com/YiJiu-Li/UnityAdbTools.git
   ```
6. 点击"Add"按钮完成安装

或者，您也可以在项目的`manifest.json`文件中添加以下依赖:

```json
{
  "dependencies": {
    "com.yijiu.adbtool": "https://github.com/YiJiu-Li/UnityAdbTools.git",
    ...
  }
}
```

## 使用方法

1. 打开ADB设备连接器: `ADB > ADB设备连接器` (快捷键 Ctrl+Alt+A)
2. 设置ADB路径（初次使用会自动检测）
3. 连接设备:
   - 通过USB连接设备并点击"检查设备IP"
   - 输入设备IP地址（或使用自动检测）
   - 点击"连接设备"按钮
4. 打包应用: `ADB > 设置包名并构建Android`
   - 选择打包模板
   - 选择要打包的场景
   - 点击"打包Android"按钮

查看[详细使用文档](Documentation~/usage.md)获取更多信息。

## 环境要求

- Unity 2021.3 或更高版本
- Android SDK 安装并配置
- 具备ADB调试权限的Android设备

## 贡献

欢迎提交Issue和Pull Request来帮助改进这个工具。

## 许可

本项目遵循MIT许可协议。详情请参阅[LICENSE](./LICENSE)文件。

## 作者

**依旧 (YiJiu-Li)**

- GitHub: [https://github.com/YiJiu-Li](https://github.com/YiJiu-Li)
