#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using AndanteTribe.Unity.Extensions;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UniTaskPlus;
using UnityEngine;

namespace Toast
{
    /// <summary>
    /// Generic toast notification display implementation.
    /// </summary>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// #nullable enable
    ///
    /// using System.Threading;
    /// using AndanteTribe.Unity.Extensions;
    /// using Cysharp.Threading.Tasks;
    /// using ObjectReference;
    /// using Toast;
    /// using UnityEngine;
    ///
    /// namespace Toast.Sample
    /// {
    ///     public class ToastPresenter : MonoBehaviour
    ///     {
    ///         [SerializeField]
    ///         private ToastControllerCore _core = new ToastControllerCore(3);
    ///
    ///         [SerializeReference]
    ///         private IObjectReference<ShortToastView> _shortToastReference = null!;
    ///
    ///         private GameObjectPool<ShortToastView> _shortToastPool = null!;
    ///
    ///         private void Awake()
    ///         {
    ///             _shortToastPool = new GameObjectPool<ShortToastView>(transform, _shortToastReference, (int)_core.MaxToastCount);
    ///             _shortToastPool.PreallocateAsync((int)_core.MaxToastCount, destroyCancellationToken).Forget();
    ///         }
    ///
    ///         /// <summary>
    ///         /// Displays a short toast message.
    ///         /// </summary>
    ///         /// <param name="message">Text expected to be a single line.</param>
    ///         /// <param name="cancellationToken">Cancellation token.</param>
    ///         public UniTask ShowShortToastAsync(string message, CancellationToken cancellationToken)
    ///         {
    ///             return _core.ShowAsync(message, _shortToastPool, static (model, firstPosY, view, _) =>
    ///             {
    ///                 view.Setup(model, firstPosY);
    ///                 return UniTask.CompletedTask;
    ///             }, cancellationToken);
    ///         }
    ///
    ///         [Button("Test1", "Hello, World!")]
    ///         [Button("Test2", "This toast is designed for text up to 19 characters.")]
    ///         private void DebugShowShortToast(string message)
    ///         {
    /// #if UNITY_EDITOR
    ///             if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
    ///             {
    ///                 return;
    ///             }
    /// #endif
    ///             ShowShortToastAsync(message, destroyCancellationToken).Forget();
    ///         }
    ///
    ///         private void OnApplicationQuit()
    ///         {
    ///             _core.Dispose();
    ///             _shortToastReference.Dispose();
    ///             _shortToastPool.Dispose();
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    [Serializable]
    public class ToastControllerCore : IDisposable
    {
        [SerializeField, Min(0), Tooltip("Vertical spacing between stacked toasts.")]
        private int _spacingY = 10;

        [SerializeField, Min(0), Tooltip("Duration of the toast show/hide animation.")]
        private float _animDuration = 0.15f;

        [SerializeField, Min(0), Tooltip("Duration for which the toast is displayed.")]
        private float _displayDuration = 1.0f;

        [SerializeField, Min(0), Tooltip("Additional display duration for the first toast to show it longer than usual.")]
        private float _firstExtraDisplayDuration = 0.3f;

        [SerializeField, Min(0), Tooltip("Wait duration when displaying toasts consecutively.")]
        private float _consecutiveWaitDuration = 0.5f;

        /// <summary>
        /// Maximum number of toasts that can be displayed simultaneously.
        /// </summary>
        public readonly uint MaxToastCount;

        /// <summary>
        /// Function to get the current timestamp. Defaults to using Time.timeAsDouble converted to ticks.
        /// </summary>
        private readonly Func<long> _getTimestamp = static () => TimeSpan.FromSeconds(Time.timeAsDouble).Ticks;

        /// <summary>
        /// Semaphore to control the number of visible toasts.
        /// </summary>
        private readonly UniTaskSemaphore _visibleSemaphore = null!;

        /// <summary>
        /// Semaphore to control the topmost toast.
        /// </summary>
        private readonly UniTaskSemaphore _topSemaphore = null!;

        /// <summary>
        /// Timestamp of the last toast invocation.
        /// </summary>
        private long _lastToastTimestamp;

        /// <summary>
        /// Timestamp when the last toast was completed.
        /// </summary>
        private long _lastToastCompletedTimestamp;

        /// <summary>
        /// Accumulated time for consecutive toast suppression.
        /// </summary>
        private TimeSpan _consecutiveAccumulatedTime;

        /// <summary>
        /// The previously displayed toast.
        /// </summary>
        /// <remarks>
        /// Expected to be null if no toasts are currently displayed.
        /// </remarks>
        private RectTransform? _previousToast;

        /// <summary>
        /// Initialize a new instance of <see cref="ToastControllerCore"/>.
        /// </summary>
        /// <param name="maxToastCount">Maximum number of toasts that can be displayed.</param>
        public ToastControllerCore(uint maxToastCount)
        {
            MaxToastCount = maxToastCount;
            _visibleSemaphore = new UniTaskSemaphore(maxToastCount, maxToastCount);
            _topSemaphore = new UniTaskSemaphore(0, maxToastCount - 1);
        }

        /// <summary>
        /// Initialize a new instance of <see cref="ToastControllerCore"/>.
        /// </summary>
        /// <param name="maxToastCount">Maximum number of toasts that can be displayed.</param>
        /// <param name="getTimestamp">Function to get the current timestamp.</param>
        public ToastControllerCore(uint maxToastCount, Func<long> getTimestamp) : this(maxToastCount)
        {
            _getTimestamp = getTimestamp;
        }

        // Default constructor for serialization.
        private ToastControllerCore()
        {
        }

        /// <summary>
        /// Displays a toast notification.
        /// </summary>
        /// <param name="model">Model object.</param>
        /// <param name="pool">Object pool.</param>
        /// <param name="toastSetup">Toast setup function.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <typeparam name="TModel">Type of the model object.</typeparam>
        /// <typeparam name="TView">Type of the toast view.</typeparam>
        public async UniTask ShowAsync<TModel, TView>(
            TModel model,
            GameObjectPool<TView> pool,
            Func<TModel, float, TView, CancellationToken, UniTask> toastSetup,
            CancellationToken cancellationToken) where TView : ToastViewBase
        {
            // Wait if the maximum display count is exceeded.
            using var _ = await _visibleSemaphore.WaitScopeAsync(cancellationToken);

            // Get the spawn index from the available slots in visibleSemaphore.
            var spawnIndex = MaxToastCount - _visibleSemaphore.CurrentCount - 1;

            // Consecutive display suppression.
            await ConsecutiveWaitAsync(cancellationToken);

            // Rent from pool and display.
            using var handle = await pool.RentScopeAsync(cancellationToken);
            var toast = handle.Instance;
            var width = toast.RectTransform.rect.width;

            // Wait if the previous toast completion animation is still running.
            var remainAnimDuration = TimeSpan.FromSeconds(_animDuration) - TimeUtils.GetElapsedTime(_lastToastCompletedTimestamp, _getTimestamp());
            if (remainAnimDuration > TimeSpan.Zero)
            {
                await UniTask.Delay(remainAnimDuration, cancellationToken: cancellationToken);
            }

            // Calculate Y position from the previously displayed toast and setup.
            var (firstPosY, previousHeight) = GetFirstPosYAndPreviousHeight(_previousToast);
            await toastSetup(model, firstPosY, toast, cancellationToken);

            // Update the previous toast reference.
            _previousToast = toast.RectTransform;

            // Check if this is the topmost toast.
            var isTopToast = previousHeight == 0;

            await LMotion.Create(-width, 0, _animDuration).BindToAnchoredPositionX(toast.RectTransform).ToUniTask(cancellationToken);

            // Continue until this becomes the topmost toast.
            for (var i = spawnIndex; i > 0 && !isTopToast; i--)
            {
                await _topSemaphore.WaitAsync(cancellationToken);

                var currentPosY = toast.RectTransform.anchoredPosition.y;
                await LMotion.Create(currentPosY, GetNextPosY(currentPosY, previousHeight), _animDuration)
                    .BindToAnchoredPositionY(toast.RectTransform).ToUniTask(cancellationToken);
            }

            // Display duration.
            var viewDuration = TimeSpan.FromSeconds(_displayDuration + (spawnIndex == 0 ? _firstExtraDisplayDuration : 0));
            await UniTask.Delay(viewDuration, cancellationToken: cancellationToken);

            await LMotion.Create(0, -width, _animDuration).BindToAnchoredPositionX(toast.RectTransform).ToUniTask(cancellationToken);

            // Clear _previousToast if this is the last toast.
            if (_visibleSemaphore.CurrentCount == MaxToastCount - 1)
            {
                _previousToast = null;
            }

            // Notify subsequent toasts of the topmost toast completion.
            var nonTopReleaseCount = MaxToastCount - 1 - _visibleSemaphore.CurrentCount;
            if (nonTopReleaseCount > 0)
            {
                _topSemaphore.Release(nonTopReleaseCount);
            }

            // Update toast completion timestamp.
            _lastToastCompletedTimestamp = _getTimestamp();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _visibleSemaphore.Dispose();
            _topSemaphore.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async UniTask ConsecutiveWaitAsync(CancellationToken cancellationToken)
        {
            var threshold = TimeSpan.FromSeconds(_consecutiveWaitDuration);
            var elapsed = TimeUtils.GetElapsedTime(_lastToastTimestamp, _lastToastTimestamp = _getTimestamp());
            var diff = threshold - elapsed;
            if (diff > TimeSpan.Zero)
            {
                var consecutiveTime = _consecutiveAccumulatedTime == TimeSpan.Zero ? diff : threshold;
                _consecutiveAccumulatedTime += consecutiveTime;
                await UniTask.Delay(_consecutiveAccumulatedTime, cancellationToken: cancellationToken);
                _consecutiveAccumulatedTime -= consecutiveTime;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (float firstPosY, float previousHeight) GetFirstPosYAndPreviousHeight(RectTransform? previousToast)
        {
            if (previousToast == null)
            {
                return (0, 0);
            }
            var previousHeight = previousToast.rect.height;
            return (previousToast.anchoredPosition.y - previousHeight - _spacingY, previousHeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetNextPosY(float currentPosY, float previousHeight)
        {
            return currentPosY + previousHeight + _spacingY;
        }
    }
}
