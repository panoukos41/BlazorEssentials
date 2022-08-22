﻿using CloudNimble.BlazorEssentials.Threading;
using CloudNimble.EasyAF.Core;
using System;
using System.Globalization;

namespace CloudNimble.BlazorEssentials
{

    /// <summary>
    /// A base class for Blazor ViewModels to implement INotifyPropertyChanged and IDisposable.
    /// </summary>
    public class BlazorObservable : EasyObservableObject
    {

        #region Private Members

        private bool disposedValue;
        private LoadingStatus loadingStatus;
        private Action stateHasChangedAction;
        private readonly DelayDispatcher delayDispatcher = new();

        #endregion

        #region Properties

        /// <summary>
        /// A <see cref="LoadingStatus"/> specifying the current state of the required data for this ViewModel.
        /// </summary>
        public LoadingStatus LoadingStatus
        {
            get => loadingStatus;
            set => Set(() => LoadingStatus, ref loadingStatus, value);
        }

        /// <summary>
        /// Allows the current Blazor container to pass the StateHasChanged action back to the BlazorObservable so ViewModel operations can 
        /// trigger state changes.
        /// </summary>
        /// <remarks>
        /// Will optionally drop intermediate StateHasChanged calls in a rapidly-updating environment, based on <see cref="StateHasChangedDelayMode"/> 
        /// and <see cref="StateHasChangedDelayInterval"/>.
        /// </remarks>
        public Action StateHasChangedAction
        {
            get
            {
                return StateHasChangedDelayMode switch
                {
                    StateHasChangedDelayMode.Debounce => () => delayDispatcher.Debounce(StateHasChangedDelayInterval, _ => StateHasChangedInternal()),
                    StateHasChangedDelayMode.Throttle => () => delayDispatcher.Throttle(StateHasChangedDelayInterval, _ => StateHasChangedInternal()),
                    _ => () => StateHasChangedInternal()
                };
            }
            set
            {
                stateHasChangedAction = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// This is public so 
        /// </remarks>
        public int StateHasChangedCount { get; set; }

        /// <summary>
        /// Flag for whether or not the render count and helpful debug feedback/warnings should be logged to the <see cref="Console"/>.
        /// Default is false.
        /// </summary>
        public StateHasChangedDebugMode StateHasChangedDebugMode { get; set; }

        /// <summary>
        /// An <see cref="int"/> specifying the number of milliseconds this BlazorObservable should wait between 
        /// <see cref="StateHasChangedAction"/> calls. Default is 100 miliseconds.
        /// </summary>
        /// <remarks>
        /// <see cref="StateHasChangedDelayMode" /> must be set to <see cref="StateHasChangedDelayMode.Debounce" /> or 
        /// <see cref="StateHasChangedDelayMode.Throttle" /> for this setting to take effect.
        /// </remarks>
        public int StateHasChangedDelayInterval { get; set; } = 100;

        /// <summary>
        /// A <see cref="StateHasChangedDelayMode" /> indicating whether or not this BlazorObservable should reduce the number of times
        /// <see cref="StateHasChangedAction" /> should be called in a given <see cref="StateHasChangedDelayInterval" />
        /// Default is <see cref="StateHasChangedDelayMode.Off"/>.
        /// </summary>
        public StateHasChangedDelayMode StateHasChangedDelayMode { get; set; } = StateHasChangedDelayMode.Off;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new instance of the <see cref="BlazorObservable" /> class.
        /// </summary>
        public BlazorObservable()
        {
           stateHasChangedAction = () =>
           {
               if (StateHasChangedDebugMode != StateHasChangedDebugMode.Off)
               {
                   Console.WriteLine($"WARNING: {GetType().Name} called empty StateHasChangedAction. Make sure to set `[YourViewModel].StateHasChangedAction = StateHasChanged;` in OnInitializedAsync()");
               }
           };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 
        /// </summary>
        internal void LogDelay()
        {
            Console.WriteLine($"{GetType().Name}: StateHasChanged #{StateHasChangedCount} called @ {DateTime.UtcNow.ToString("hh:mm:ss.fff", CultureInfo.InvariantCulture)} " +
                $"{(StateHasChangedDebugMode != StateHasChangedDebugMode.Off ? $"after {delayDispatcher.DelayCount} dropped calls." : "")}");

            if (StateHasChangedDebugMode != StateHasChangedDebugMode.Tuning || StateHasChangedDelayMode == StateHasChangedDelayMode.Off) return;

            var diffMiliseconds = DateTime.UtcNow.Subtract(delayDispatcher.TimerStarted).TotalMilliseconds;

            // RWM: We're going to use a Tuple switch statement to simplify 
            var entry = (StateHasChangedDelayMode, StateHasChangedDelayInterval, delayDispatcher.DelayCount, diffMiliseconds) switch
            {
                (StateHasChangedDelayMode.Debounce, _, _, < 50) => $"Performance: Debounce waited {diffMiliseconds}ms between calls. Delay was imperceptible.",

                (StateHasChangedDelayMode.Debounce, _, _, < 2000) => $"Performance: Debounce waited {diffMiliseconds}ms between calls. Delay was noticeable.",

                (StateHasChangedDelayMode.Throttle, < 50, _, _) => $"Performance: Throttle waited {StateHasChangedDelayInterval}ms between calls. Delay was imperceptible.",

                (StateHasChangedDelayMode.Throttle, < 2000, < 10, _) => $"Performance: Throttle waited {StateHasChangedDelayInterval}ms between calls," +
                    $" but there were fewer than 10 calls dropped. Delay was imperceptible, but consider using Debounce instead.",

                (StateHasChangedDelayMode.Throttle, < 2000, _, _) => $"Performance: Throttle waited {StateHasChangedDelayInterval}ms between calls." +
                    $" If your goal is to reduce the number of repaints but fire them consistently, this is the right setting.",

                _ => $"Performance: {StateHasChangedDelayMode} waited {(StateHasChangedDelayMode == StateHasChangedDelayMode.Debounce ? diffMiliseconds : StateHasChangedDelayInterval)}ms " +
                    $"between calls. Delay was unacceptable. Consider adding a visual 'waiting' indicator for the end user."
            };

            //if (string.IsNullOrWhiteSpace(entry)) return;
            Console.WriteLine(entry);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// RWM: DO NOT change this method. Doing anything other than returning the StateHasChangedAction
        /// will cause an infinite loop!
        /// </remarks>
        internal Action StateHasChangedInternal()
        {
            ++StateHasChangedCount;

            if (StateHasChangedDebugMode != StateHasChangedDebugMode.Off)
            {
                LogDelay();
            }

            return stateHasChangedAction;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    delayDispatcher.Dispose();
                }

                disposedValue = true;
                base.Dispose(disposing);
            }
        }

        #endregion

    }

}
