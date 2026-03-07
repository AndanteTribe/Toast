# Toast
[![Releases](https://img.shields.io/github/release/AndanteTribe/Toast.svg)](https://github.com/AndanteTribe/Toast/releases)
[![GitHub license](https://img.shields.io/github/license/AndanteTribe/Toast.svg)](./LICENSE)

English | [日本語](README_JA.md)

## Overview
**Toast** is a simple toast notification UI library for Unity.

It provides a core controller and base view class that let you display stacked, animated toast messages with configurable display duration, animation speed, spacing, and consecutive display suppression.

## Requirements
- Unity 6000.0 or later
- [UniTask](https://github.com/Cysharp/UniTask) 2.5.10 or later
- [LitMotion](https://github.com/annulusgames/LitMotion) 2.0.1 or later
- [GameObjectPool](https://github.com/AndanteTribe/GameObjectPool) 0.1.1 or later
- [UniTaskPlus](https://github.com/AndanteTribe/UniTaskPlus) 0.1.2 or later

## Installation
Open `Window > Package Manager`, select `[+] > Add package from git URL`, and enter the following URL:

```
https://github.com/AndanteTribe/Toast.git?path=src/Toast.Unity/Packages/jp.andantetribe.toast
```

## Quick Start

1. Create a custom view class that inherits from `ToastViewBase`.
2. Create a `Presenter` MonoBehaviour that holds a `ToastControllerCore` and a `GameObjectPool` for your view.
3. Call `ShowAsync` to display a toast.

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

## Editor Extension

When `ToastControllerCore` is serialized as a field on a MonoBehaviour (e.g. `ToastPresenter`), the included custom property drawer renders its settings in a labeled box in the Unity Inspector:

![ToastPresenter Inspector](https://github.com/user-attachments/assets/9e52ccaa-2024-43d9-b0d9-20b05ce78708)

| Inspector Field | Description |
|----------------|-------------|
| Spacing Y | Vertical spacing between stacked toasts. |
| Anim Duration | Duration of the show/hide animation (seconds). |
| Display Duration | Duration for which the toast is visible (seconds). |
| First Extra Display Duration | Additional display time for the first toast in a sequence (seconds). |
| Consecutive Wait Duration | Wait time applied when toasts are shown in rapid succession (seconds). |

## API

### `ToastControllerCore`

| Member | Description |
|--------|-------------|
| `MaxToastCount` | Maximum number of toasts that can be displayed simultaneously. |
| `ShowAsync<TModel, TView>(TModel model, GameObjectPool<TView> pool, Func<TModel, float, TView, CancellationToken, UniTask> toastSetup, CancellationToken cancellationToken)` | Displays a toast notification. Waits if the maximum display count is exceeded. |
| `Dispose()` | Releases internal semaphore resources. |

### `ToastViewBase`

| Member | Description |
|--------|-------------|
| `RectTransform` | Gets the `RectTransform` component attached to this GameObject (cached). |

## License
This library is released under the MIT license.
