using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections;
using System.Collections.Generic;
using TofAr.V0;
using TofAr.V0.Hand;
using UnityEngine;

namespace MixedReality.Toolkit.TofAr.Input
{
    [MixedRealityController(
        SupportedControllerType.ArticulatedHand,
        new[] { Handedness.Left, Handedness.Right })]
    public class TofArHand : BaseController, IMixedRealityHand
    {
        protected Vector3 CurrentControllerPosition = Vector3.zero;
        protected Quaternion CurrentControllerRotation = Quaternion.identity;
        protected MixedRealityPose CurrentControllerPose = MixedRealityPose.ZeroIdentity;

        private Vector3 currentPointerPosition = Vector3.zero;
        private Quaternion currentPointerRotation = Quaternion.identity;
        private MixedRealityPose currentPointerPose = MixedRealityPose.ZeroIdentity;
        private MixedRealityPose currentIndexPose = MixedRealityPose.ZeroIdentity;
        private MixedRealityPose currentGripPose = MixedRealityPose.ZeroIdentity;
        private MixedRealityPose lastGripPose = MixedRealityPose.ZeroIdentity;

        private readonly HandRay handRay = new HandRay();

        private Vector3 scaleOffset = Vector3.zero;

        private ScreenOrientation currentScreenOrientation;
        private Quaternion baseRotation = Quaternion.identity;

        private FixedRingBuffer<bool> handPinchingHistory = null;
        private PoseIndex[] PinchTargetPoses => new[]
        {
            PoseIndex.Heart, PoseIndex.PreSnap, PoseIndex.Fist
        };


        public TofArHand(TrackingState trackingState, Handedness controllerHandedness, IMixedRealityInputSource inputSource = null, MixedRealityInteractionMapping[] interactions = null)
            : base(trackingState, controllerHandedness, inputSource, interactions)
        {
            this.handPinchingHistory = new FixedRingBuffer<bool>(5);
        }

        public override MixedRealityInteractionMapping[] DefaultInteractions => new[]
        {
            new MixedRealityInteractionMapping(0, "Spatial Pointer", AxisType.SixDof, DeviceInputType.SpatialPointer, new MixedRealityInputAction(4, "Pointer Pose", AxisType.SixDof)),
            new MixedRealityInteractionMapping(1, "Spatial Grip", AxisType.SixDof, DeviceInputType.SpatialGrip, new MixedRealityInputAction(3, "Grip Pose", AxisType.SixDof)),
            new MixedRealityInteractionMapping(2, "Select", AxisType.Digital, DeviceInputType.Select, new MixedRealityInputAction(1, "Select", AxisType.Digital)),
            new MixedRealityInteractionMapping(3, "Grab", AxisType.SingleAxis, DeviceInputType.TriggerPress, new MixedRealityInputAction(7, "Grip Press", AxisType.SingleAxis)),
            new MixedRealityInteractionMapping(4, "Index Finger Pose", AxisType.SixDof, DeviceInputType.IndexFinger,  new MixedRealityInputAction(13, "Index Finger Pose", AxisType.SixDof)),
        };

        public override MixedRealityInteractionMapping[] DefaultLeftHandedInteractions => DefaultInteractions;

        public override MixedRealityInteractionMapping[] DefaultRightHandedInteractions => DefaultInteractions;


        #region IMixedRealityHand Implementation

        public bool TryGetJoint(TrackedHandJoint joint, out MixedRealityPose pose)
        {
            return jointPoses.TryGetValue(joint, out pose);
        }

        #endregion IMixedRealityHand Implementation

        public override bool IsInPointingPose
        {
            get
            {
                return true;
            }
        }

        protected bool IsPinching { set; get; }

        protected Vector3 GetPalmNormal()
        {
            var result = Vector3.zero;
            if (TryGetJoint(TrackedHandJoint.Palm, out MixedRealityPose pose))
            {
                result = -pose.Up;
            }
            //Debug.Log($"GetPalmNormal()={result}");
            return result;
        }

        public void UpdateController(Vector3[] points, HandData handData, GestureResultProperty gestureResult)
        {
            if (!Enabled)
            {
                return;
            }
            if (points == null || points.Length == 0)
            {
                return;
            }

            if (!UnityEngine.XR.XRSettings.enabled)
            {
                if (!UnityEngine.XR.XRSettings.enabled)
                {
                    var screenOrientation = Screen.orientation;

                    if (currentScreenOrientation != screenOrientation)
                    {
                        currentScreenOrientation = screenOrientation;

                        int imageRotation = 0;
                        switch (screenOrientation)
                        {
                            case ScreenOrientation.Portrait:
                                imageRotation = 270;
                                break;
                            case ScreenOrientation.PortraitUpsideDown:
                                imageRotation = 90;
                                break;
                            case ScreenOrientation.LandscapeLeft:
                                imageRotation = 0;
                                break;
                            case ScreenOrientation.LandscapeRight:
                                imageRotation = 180;
                                break;
                            default:
                                break;
                        }
                        this.baseRotation = Quaternion.Euler(0f, 0f, imageRotation);
                    }
                }
            }

            this.UpdateHandData(points, handData, gestureResult);

            lastGripPose = currentGripPose;

            Vector3 pointerPosition = jointPoses[TrackedHandJoint.Palm].Position;
            IsPositionAvailable = IsRotationAvailable = pointerPosition != Vector3.zero;

            if (IsPositionAvailable)
            {
                handRay.Update(pointerPosition, GetPalmNormal(), CameraCache.Main.transform, ControllerHandedness);

                var ray = handRay.Ray;

                currentPointerPose.Position = ray.origin;
                currentPointerPose.Rotation = Quaternion.LookRotation(ray.direction);

                currentGripPose = jointPoses[TrackedHandJoint.Palm];
            }

            if (lastGripPose != currentGripPose)
            {
                if (IsPositionAvailable && IsRotationAvailable)
                {
                    CoreServices.InputSystem?.RaiseSourcePoseChanged(InputSource, this, currentGripPose);
                }
                else if (IsPositionAvailable && !IsRotationAvailable)
                {
                    CoreServices.InputSystem?.RaiseSourcePositionChanged(InputSource, this, currentPointerPosition);
                }
                else if (!IsPositionAvailable && IsRotationAvailable)
                {
                    CoreServices.InputSystem?.RaiseSourceRotationChanged(InputSource, this, currentPointerRotation);
                }
            }

            for (int i = 0; i < Interactions?.Length; i++)
            {
                switch (Interactions[i].InputType)
                {
                    case DeviceInputType.SpatialPointer:
                        Interactions[i].PoseData = currentPointerPose;
                        if (Interactions[i].Changed)
                        {
                            CoreServices.InputSystem?.RaisePoseInputChanged(InputSource, ControllerHandedness, Interactions[i].MixedRealityInputAction, currentPointerPose);
                        }
                        break;
                    case DeviceInputType.SpatialGrip:
                        Interactions[i].PoseData = currentGripPose;
                        if (Interactions[i].Changed)
                        {
                            CoreServices.InputSystem?.RaisePoseInputChanged(InputSource, ControllerHandedness, Interactions[i].MixedRealityInputAction, currentGripPose);
                        }
                        break;
                    case DeviceInputType.Select:
                        TofArManager.Logger.WriteLog(LogLevel.Debug, $"IsPinching={IsPinching}");
                        Interactions[i].BoolData = IsPinching;

                        if (Interactions[i].Changed)
                        {
                            if (Interactions[i].BoolData)
                            {
                                CoreServices.InputSystem?.RaiseOnInputDown(InputSource, ControllerHandedness, Interactions[i].MixedRealityInputAction);
                            }
                            else
                            {
                                CoreServices.InputSystem?.RaiseOnInputUp(InputSource, ControllerHandedness, Interactions[i].MixedRealityInputAction);
                            }
                        }
                        break;
                    case DeviceInputType.TriggerPress:
                        Interactions[i].BoolData = IsPinching;

                        if (Interactions[i].Changed)
                        {
                            if (Interactions[i].BoolData)
                            {
                                CoreServices.InputSystem?.RaiseOnInputDown(InputSource, ControllerHandedness, Interactions[i].MixedRealityInputAction);
                            }
                            else
                            {
                                CoreServices.InputSystem?.RaiseOnInputUp(InputSource, ControllerHandedness, Interactions[i].MixedRealityInputAction);
                            }
                        }
                        break;
                    case DeviceInputType.IndexFinger:
                        UpdateIndexFingerData(Interactions[i]);
                        break;
                }
            }
        }

        protected readonly Dictionary<TrackedHandJoint, MixedRealityPose> jointPoses = new Dictionary<TrackedHandJoint, MixedRealityPose>();

        protected void UpdateHandData(Vector3[] points, HandData handData, GestureResultProperty gestureResult)
        {
            if (points != null)
            {
                float scaleFactor = 1f;
                Vector3 center = (points[(int)HandPointIndex.WristPinkySide] + points[(int)HandPointIndex.WristThumbSide]) / 2f;
                this.scaleOffset = (center * (1 - 1 / scaleFactor));

                this.UpdateJoint(points[(int)HandPointIndex.HandCenter], points[(int)HandPointIndex.MidRoot] - center, TrackedHandJoint.Palm);

                var wristPosition = center;
                this.UpdateJoint(wristPosition, points[(int)HandPointIndex.ArmCenter], TrackedHandJoint.Wrist);

                this.UpdateJoint(points[(int)HandPointIndex.HandCenter], wristPosition, TrackedHandJoint.ThumbMetacarpalJoint);
                this.UpdateJoint(points[(int)HandPointIndex.HandCenter], wristPosition, TrackedHandJoint.IndexMetacarpal);
                this.UpdateJoint(points[(int)HandPointIndex.HandCenter], wristPosition, TrackedHandJoint.MiddleMetacarpal);
                this.UpdateJoint(points[(int)HandPointIndex.HandCenter], wristPosition, TrackedHandJoint.RingMetacarpal);
                this.UpdateJoint(points[(int)HandPointIndex.HandCenter], wristPosition, TrackedHandJoint.PinkyMetacarpal);

                this.UpdateJoint(points[(int)HandPointIndex.ThumbRoot], points[(int)HandPointIndex.HandCenter], TrackedHandJoint.ThumbProximalJoint);
                this.UpdateJoint(points[(int)HandPointIndex.ThumbJoint], points[(int)HandPointIndex.ThumbRoot], TrackedHandJoint.ThumbDistalJoint);
                this.UpdateJoint(points[(int)HandPointIndex.ThumbTip], points[(int)HandPointIndex.ThumbJoint], TrackedHandJoint.ThumbTip);

                this.UpdateJoint(points[(int)HandPointIndex.IndexRoot], points[(int)HandPointIndex.HandCenter], TrackedHandJoint.IndexKnuckle);
                this.UpdateJoint(points[(int)HandPointIndex.IndexJoint], points[(int)HandPointIndex.IndexRoot], TrackedHandJoint.IndexMiddleJoint);
                this.UpdateJoint(points[(int)HandPointIndex.IndexTip], points[(int)HandPointIndex.IndexJoint], TrackedHandJoint.IndexTip);

                this.UpdateJoint(points[(int)HandPointIndex.MidRoot], points[(int)HandPointIndex.HandCenter], TrackedHandJoint.MiddleKnuckle);
                this.UpdateJoint(points[(int)HandPointIndex.MidJoint], points[(int)HandPointIndex.MidRoot], TrackedHandJoint.MiddleMiddleJoint);
                this.UpdateJoint(points[(int)HandPointIndex.MidTip], points[(int)HandPointIndex.MidJoint], TrackedHandJoint.MiddleTip);

                this.UpdateJoint(points[(int)HandPointIndex.RingRoot], points[(int)HandPointIndex.HandCenter], TrackedHandJoint.RingKnuckle);
                this.UpdateJoint(points[(int)HandPointIndex.RingJoint], points[(int)HandPointIndex.RingRoot], TrackedHandJoint.RingMiddleJoint);
                this.UpdateJoint(points[(int)HandPointIndex.RingTip], points[(int)HandPointIndex.RingJoint], TrackedHandJoint.RingTip);

                this.UpdateJoint(points[(int)HandPointIndex.PinkyRoot], points[(int)HandPointIndex.HandCenter], TrackedHandJoint.PinkyKnuckle);
                this.UpdateJoint(points[(int)HandPointIndex.PinkyJoint], points[(int)HandPointIndex.PinkyRoot], TrackedHandJoint.PinkyMiddleJoint);
                this.UpdateJoint(points[(int)HandPointIndex.PinkyTip], points[(int)HandPointIndex.PinkyJoint], TrackedHandJoint.PinkyTip);


                var wrist = this.jointPoses[TrackedHandJoint.Wrist].Position;
                var indexKnuckle = this.jointPoses[TrackedHandJoint.IndexKnuckle].Position;
                var pinkyKnuckle = this.jointPoses[TrackedHandJoint.PinkyKnuckle].Position;

                var wristToIndex = indexKnuckle - wrist;
                var wristToPinky = pinkyKnuckle - wrist;
                var palmDirection = (wristToIndex + wristToPinky) / 2f;
                var palmNormal = Vector3.Cross(wristToIndex, wristToPinky);

                var palmPose = this.jointPoses[TrackedHandJoint.Palm];
                palmPose.Rotation = Quaternion.LookRotation(palmDirection, palmNormal);
                this.jointPoses[TrackedHandJoint.Palm] = palmPose;

                var poseIndexLeft = PoseIndex.None;
                var poseIndexRight = PoseIndex.None;
                handData?.GetPoseIndex(out poseIndexLeft, out poseIndexRight);
                var pinching = false;
                if ((this.ControllerHandedness == Handedness.Right) || (this.ControllerHandedness == Handedness.Both))
                {
                    foreach (var p in this.PinchTargetPoses)
                    {
                        if (poseIndexRight == p)
                        {
                            pinching = true;
                            break;
                        }
                    }
                }
                if ((this.ControllerHandedness == Handedness.Left) || (this.ControllerHandedness == Handedness.Both))
                {
                    foreach (var p in this.PinchTargetPoses)
                    {
                        if (poseIndexLeft == p)
                        {
                            pinching = true;
                            break;
                        }
                    }
                }
                this.handPinchingHistory.Enqueue(pinching);
                var pinchingCount = 0;
                foreach (var p in this.handPinchingHistory)
                {
                    pinchingCount = p ? pinchingCount + 1 : pinchingCount;

                }
                this.IsPinching = (pinchingCount > this.handPinchingHistory.MaxCapacity / 10);

                CoreServices.InputSystem?.RaiseHandJointsUpdated(InputSource, ControllerHandedness, this.jointPoses);
            }
        }

        private void UpdateJoint(Vector3 jointPoint, Vector3? prevJointPoint, TrackedHandJoint storeTarget)
        {
            var rotation = Quaternion.identity;
            if (prevJointPoint != null)
            {
                rotation = Quaternion.FromToRotation((Vector3)prevJointPoint, jointPoint);
            }
            if (jointPoint.z <= 0)
            {
                this.UpdateJoint(jointPoint, rotation, storeTarget);
            }
            else
            {
                this.UpdateJoint(jointPoint - this.scaleOffset, rotation, storeTarget);
            }
        }

        protected void UpdateJoint(Vector3 position, Quaternion rotation, TrackedHandJoint storeTarget)
        {
            var localMatrix = Matrix4x4.TRS(Vector3.zero, this.baseRotation, Vector3.one);
            var worldMatrix = Camera.main.transform.localToWorldMatrix * localMatrix;
            position = worldMatrix.MultiplyPoint(position);

            var pose = new MixedRealityPose(position, rotation);
            //if (position.z > 0f)
            //{
                if (!this.jointPoses.ContainsKey(storeTarget))
                {
                    this.jointPoses.Add(storeTarget, pose);
                }
                else
                {
                    this.jointPoses[storeTarget] = pose;
                }
            //}
        }

        private void UpdateIndexFingerData(MixedRealityInteractionMapping interactionMapping)
        {
            if (this.jointPoses.TryGetValue(TrackedHandJoint.IndexTip, out var pose))
            {
                currentIndexPose.Rotation = pose.Rotation;
                currentIndexPose.Position = pose.Position;
            }

            interactionMapping.PoseData = currentIndexPose;

            // If our value changed raise it.
            if (interactionMapping.Changed)
            {
                // Raise input system Event if it enabled
                CoreServices.InputSystem?.RaisePoseInputChanged(InputSource, ControllerHandedness, interactionMapping.MixedRealityInputAction, currentIndexPose);
            }
        }
    }

    public class FixedRingBuffer<T> : IEnumerable<T>
    {
        private Queue<T> _queue;

        public int Count => this._queue.Count;

        public int MaxCapacity { get; private set; }

        public FixedRingBuffer(int maxCapacity)
        {
            this.MaxCapacity = maxCapacity;
            this._queue = new Queue<T>(maxCapacity);
        }

        public void Enqueue(T item)
        {
            this._queue.Enqueue(item);

            if (this._queue.Count > this.MaxCapacity)
            {
                T removed = this.Dequeue();
            }
        }

        public T Dequeue() => this._queue.Dequeue();

        public T Peek() => this._queue.Peek();

        public IEnumerator<T> GetEnumerator() => this._queue.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this._queue.GetEnumerator();
    }
}
