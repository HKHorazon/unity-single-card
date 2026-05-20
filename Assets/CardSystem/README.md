# CardSystem — Unity URP 3D Card Visual System

## 依賴（移轉前請先安裝）
1. **TextMeshPro** — Window > TextMeshPro > Import TMP Essential Resources
2. **DOTween** — Asset Store 或 `com.demigiant.dotween` Package Manager
3. **URP** — Universal Render Pipeline 已設定於目標專案

## 移轉步驟
1. 複製整個 `Assets/CardSystem/` 資料夾至目標專案
2. 場景中建立空 GameObject，掛上 `CardTextSnapshotManager` 元件
3. 在 Inspector 設定 `snapshotResolution`（預設 512）
4. 使用 `Card_Example.prefab` 作為卡牌起點
5. 呼叫 `cardController.SetCardData(...)` 設定卡牌資料

## 公開 API 速查
### CardTextSnapshotManager
- `Texture2D GenerateTextSnapshot(string cardName, string description, int cost, int attack, int health)`
- `void ReleaseSnapshot(Texture2D texture)`

### CardController
- `void SetCardData(string cardName, string description, int cost, int attack, int health)`
- `void Flip(float duration = 0.5f)`
- `void PlayDissolve(float duration, System.Action onComplete = null)`

## 匯出 Unity Package
Assets > Export Package > 選取 CardSystem 資料夾 > Export
