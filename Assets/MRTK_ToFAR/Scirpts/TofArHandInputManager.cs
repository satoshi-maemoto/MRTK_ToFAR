using TofAr.V0.Hand;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TofAr.V0;

namespace MixedReality.Toolkit.TofAr.Input
{
    /// <summary>
    /// Manages ToF AR Hand Inputs
    /// </summary>
    [MixedRealityDataProvider(
        typeof(IMixedRealityInputSystem),
        SupportedPlatforms.Android | SupportedPlatforms.WindowsEditor | SupportedPlatforms.MacEditor | SupportedPlatforms.LinuxEditor,
        "ToF AR Hand Input Manager")]
    public class TofArHandInputManager : BaseInputDeviceManager, IMixedRealityCapabilityCheck
    {
        private Dictionary<Handedness, TofArHand> trackedHands = new Dictionary<Handedness, TofArHand>();

        private HandStatus handStatus;
        private Vector3[] handPointsLeft = null;
        private Vector3[] handPointsRight = null;
        private HandData handData = null;
        private GestureResultProperty gestureResult = new GestureResultProperty();

        public TofArHandInputManager(
            IMixedRealityInputSystem inputSystem,
            string name = null,
            uint priority = DefaultPriority,
            BaseMixedRealityProfile profile = null) : base(inputSystem, name, priority, profile) {
        }

        public override void Enable()
        {
            base.Enable();

            HandCalc.OnLeftHandPointsCalculated += this.OnHandsCalculatedLeft;
            HandCalc.OnRightHandPointsCalculated += OnHandsCalculatedRight;
            TofArHandManager.OnGestureEstimated += (s, result) =>
            {
                this.gestureResult = result;
            };
        }

        private void OnHandsCalculatedLeft(Vector3[] points, HandStatus handStatus)
        {
            this.handStatus = handStatus;
            if (points == null || points.Length == 0)
            {
                return;
            }

            if (this.handPointsLeft == null || this.handPointsLeft.Length != points.Length)
            {
                this.handPointsLeft = new Vector3[points.Length];
            }
            Array.Copy(points, this.handPointsLeft, points.Length);
            this.handData = TofArHandManager.Instance.HandData;
        }

        private void OnHandsCalculatedRight(Vector3[] points, HandStatus handStatus)
        {
            this.handStatus = handStatus;
            if (points == null || points.Length == 0)
            {
                return;
            }

            if (this.handPointsRight == null || this.handPointsRight.Length != points.Length)
            {
                this.handPointsRight = new Vector3[points.Length];
            }
            Array.Copy(points, this.handPointsRight, points.Length);
            this.handData = TofArHandManager.Instance.HandData;
        }

        public override void Disable()
        {
            base.Disable();

            IMixedRealityInputSystem inputSystem = Service as IMixedRealityInputSystem;

            foreach (var hand in trackedHands)
            {
                if (hand.Value != null)
                {
                    inputSystem?.RaiseSourceLost(hand.Value.InputSource, hand.Value);
                }
            }

            trackedHands.Clear();
        }

        public override IMixedRealityController[] GetActiveControllers()
        {
            return trackedHands.Values.ToArray<IMixedRealityController>();
        }

        public bool CheckCapability(MixedRealityCapability capability)
        {
            return (capability == MixedRealityCapability.ArticulatedHand);
        }

        public override void Update()
        {
            base.Update();
            this.UpdateHand(this.handPointsRight, Handedness.Right);
            this.UpdateHand(this.handPointsLeft, Handedness.Left);
        }

        protected void UpdateHand(Vector3[] points, Handedness handedness)
        {
            if (
                ((handedness == Handedness.Right) || (handedness == Handedness.Both)) && ((this.handStatus == HandStatus.RightHand) || (this.handStatus == HandStatus.BothHands)) ||
                ((handedness == Handedness.Left) || (handedness == Handedness.Both)) && ((this.handStatus == HandStatus.LeftHand) || (this.handStatus == HandStatus.BothHands))
                )
            {
                var hand = GetOrAddHand(handedness);
                hand.UpdateController(points, this.handData, this.gestureResult);
            }
            else
            {
                RemoveHandDevice(handedness);
            }
        }

        private TofArHand GetOrAddHand(Handedness handedness)
        {
            if (trackedHands.ContainsKey(handedness))
            {
                return trackedHands[handedness];
            }

            // Add new hand
            var pointers = RequestPointers(SupportedControllerType.ArticulatedHand, handedness);
            var inputSourceType = InputSourceType.Hand;
            var inputSource = Service?.RequestNewGenericInputSource($"TofAr {handedness} Hand", pointers, inputSourceType);

            var controller = new TofArHand(TrackingState.Tracked, handedness, inputSource);
            for (int i = 0; i < controller.InputSource?.Pointers?.Length; i++)
            {
                controller.InputSource.Pointers[i].Controller = controller;
            }

            Service?.RaiseSourceDetected(controller.InputSource, controller);

            trackedHands.Add(handedness, controller);

            return controller;
        }

        private int lostFrames = 0;
        private const int FramesForRemoveHand = 30;

        private void RemoveHandDevice(Handedness handedness)
        {
            if (trackedHands.ContainsKey(handedness))
            {
                this.lostFrames++;
                if (this.lostFrames < FramesForRemoveHand)
                {
                    return;
                }

                var hand = trackedHands[handedness];
                CoreServices.InputSystem?.RaiseSourceLost(hand.InputSource, hand);
                trackedHands.Remove(handedness);
                TofArManager.Logger.WriteLog(LogLevel.Debug, $"trackedHands.Remove");
            }
        }

        private void RemoveAllHandDevices()
        {
            foreach (var controller in trackedHands.Values)
            {
                CoreServices.InputSystem?.RaiseSourceLost(controller.InputSource, controller);
            }
            trackedHands.Clear();
        }
    }
}
