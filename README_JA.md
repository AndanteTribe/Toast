# Toast
[![Releases](https://img.shields.io/github/release/AndanteTribe/Toast.svg)](https://github.com/AndanteTribe/Toast/releases)
[![GitHub license](https://img.shields.io/github/license/AndanteTribe/Toast.svg)](./LICENSE)

[English](README.md) | 日本語

## 概要
**Toast** は、Unity 向けのシンプルなトースト通知UIライブラリです。

コアコントローラーとベースビュークラスを提供しており、表示時間・アニメーション速度・間隔・連続表示抑制などを設定可能なスタック型アニメーション付きトーストメッセージを表示できます。

## 要件
- Unity 6000.0 以上
- [UniTask](https://github.com/Cysharp/UniTask) 2.5.10 以上
- [LitMotion](https://github.com/annulusgames/LitMotion) 2.0.1 以上
- [GameObjectPool](https://github.com/AndanteTribe/GameObjectPool) 0.1.1 以上
- [UniTaskPlus](https://github.com/AndanteTribe/UniTaskPlus) 0.1.2 以上

## インストール
`Window > Package Manager` からPackage Managerウィンドウを開き、`[+] > Add package from git URL` を選択して以下のURLを入力します。

```
https://github.com/AndanteTribe/Toast.git?path=src/Toast.Unity/Packages/jp.andantetribe.toast
```

## クイックスタート

1. `ToastViewBase` を継承したカスタムビュークラスを作成します。
2. `ToastControllerCore` と、ビュー用の `GameObjectPool` を保持する `Presenter` MonoBehaviour を作成します。
3. `ShowAsync` を呼び出してトーストを表示します。

```csharp
#nullable enable

using System.Threading;
using AndanteTribe.Unity.Extensions;
using Cysharp.Threading.Tasks;
using ObjectReference;
using Toast;
using UnityEngine;

namespace Toast.Sample
{
    public class ToastPresenter : MonoBehaviour
    {
        [SerializeField]
        private ToastControllerCore _core = new ToastControllerCore(3);

        [SerializeReference]
        private IObjectReference<ShortToastView> _shortToastReference = null!;

        private GameObjectPool<ShortToastView> _shortToastPool = null!;

        private void Awake()
        {
            _shortToastPool = new GameObjectPool<ShortToastView>(transform, _shortToastReference, (int)_core.MaxToastCount);
            _shortToastPool.PreallocateAsync((int)_core.MaxToastCount, destroyCancellationToken).Forget();
        }

        public UniTask ShowShortToastAsync(string message, CancellationToken cancellationToken)
        {
            return _core.ShowAsync(message, _shortToastPool, static (model, firstPosY, view, _) =>
            {
                view.Setup(model, firstPosY);
                return UniTask.CompletedTask;
            }, cancellationToken);
        }

        private void OnApplicationQuit()
        {
            _core.Dispose();
            _shortToastReference.Dispose();
            _shortToastPool.Dispose();
        }
    }
}
```

## API

### `ToastControllerCore`

| メンバー | 説明 |
|--------|------|
| `MaxToastCount` | 同時に表示できるトーストの最大数。 |
| `ShowAsync<TModel, TView>(TModel model, GameObjectPool<TView> pool, Func<TModel, float, TView, CancellationToken, UniTask> toastSetup, CancellationToken cancellationToken)` | トースト通知を表示します。最大表示数を超えた場合は待機します。 |
| `Dispose()` | 内部セマフォリソースを解放します。 |

### `ToastViewBase`

| メンバー | 説明 |
|--------|------|
| `RectTransform` | このGameObjectにアタッチされた `RectTransform` コンポーネントを返します（キャッシュされます）。 |

## ライセンス
このライブラリは、MITライセンスで公開しています。