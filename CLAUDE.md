# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 唯一目標

實作 `Design/Unity_3D_Card_System_Spec.md` 規格的高效能 3D 卡牌系統。所有決策皆以此規格為準。

## 技術環境

- **Unity 版本**: 6000.3.14f1 (Unity 6 LTS)
- **渲染管線**: URP 17.3.0（非 HDRP、非 Built-in）
- **目標套件**: TextMeshPro、DOTween（需透過 Package Manager 匯入）
- **平台設定**: `Assets/Settings/` 內有 PC 與 Mobile 分離的 Renderer/RPAsset

## 核心架構（三大模組）

### 1. `CardTextSnapshotManager.cs`
- Singleton，管理一個 Off-screen Camera + 共用 `RenderTexture`（512×512, ARGB32）
- 提供 `GenerateTextSnapshot(...)` → 回傳 `Texture2D`（內建 Object Pool 避免每張卡獨占 RT）
- 卡牌 `Destroy` 時必須顯式 `Destroy(texture2D)` 釋放 VRAM

### 2. `CardController.cs`
- 初始化呼叫 `CardTextSnapshotManager` 取得文字貼圖，注入材質 `_TextTex`
- 材質狀態分兩種：**Static**（無互動時關閉視差省排程）、**Dynamic**（懸停/翻面/溶解時開完整功能）
- 滑鼠懸停：`OnMouseEnter/Over/Exit` + `Quaternion.Lerp` 傾斜 ±15°
- 翻面：DOTween 驅動 Y 軸旋轉，90° 時切換 `_IsFaceUp`
- 溶解：`PlayDissolve(float duration, Action onComplete)` DOTween 漸變 `_DissolveAmount`

### 3. `CardParallaxDissolve.shader`
- URP Unlit 基礎，頂點著色器傳遞切線空間視點向量（TBN × V_world）
- 片段著色器：視差偏移 → 四層混色（BG → Char → Frame → Text）→ 掃光疊加 → 溶解 clip
- 溶解邊緣可選發光燒焦效果

## 效能規範（強制）

- 所有 `Shader.PropertyToID` 必須在 `Awake` 中預先快取
- 禁止在 `Update` 或 `OnMouseOver` 中使用字串傳參（如 `material.SetFloat("_DissolveAmount", ...)`）
- 目標：場上同時 30–50 張卡不爆 Draw Call / VRAM

## 工具使用

使用 `unity-skills` skill 操作 Unity Editor（建立 Script、場景操作、Asset 管理）。實作前先確認 Unity Editor MCP 連線正常。
