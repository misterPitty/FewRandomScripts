#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using Google.XR.ARCoreExtensions;
using ModestTree;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using XSight.Scripts.Adventures;
using System.Collections;

namespace XSight.Scripts.Geospatial
{
    public class GeoSpatialCore : MonoBehaviour, IGeoSpatialService
    {
        public bool ArcoreGeospatialIsTracking { get; private set; }
        public event Action OnArcoreGeospatialTrackingStatusChanged;

        [SerializeField] GameObject _arSessionOrigin;
        [SerializeField] AREarthManager _arEarthManager;
        [SerializeField] ARAnchorManager _anchorManager;
        [SerializeField] private GameObject _poiHologramPrefab;
        [SerializeField] private float _delayForDefaultAnchorGet = 10f;
        [SerializeField] private int _randomLocationArea = 20;

        private bool _arcoreGeospatialIsSupported = true;

        private List<string> _arModelsWaitingForAnchor = new();
        private List<AnchorReadyObject> _anchorReadyObjects = new();
        private WaitForSeconds _delayForDefaultAnchor;

        private readonly LogHelper _logHelper = new LogHelper(true);

        private void Start()
        {
            _delayForDefaultAnchor = new WaitForSeconds(_delayForDefaultAnchorGet);

            if (_arEarthManager.IsGeospatialModeSupported(GeospatialMode.Enabled) == FeatureSupported.Supported)
            {
                _arcoreGeospatialIsSupported = true;
            }

            _logHelper.LogDebug("GeoSpatial is supported = "+ _arcoreGeospatialIsSupported);
        }

        private void Update()
        {
            if (_arcoreGeospatialIsSupported)
            {
                if (!ArcoreGeospatialIsTracking && _arEarthManager.EarthTrackingState == TrackingState.Tracking)
                {
                    ArcoreGeospatialIsTracking = true;
                    OnArcoreGeospatialTrackingStatusChanged?.Invoke();

                    _logHelper.LogDebug($"GeoSpatial is tracking: trackingState = {_arEarthManager.EarthTrackingState}, earthState = {_arEarthManager.EarthState}");
                }
                else if (ArcoreGeospatialIsTracking && _arEarthManager.EarthTrackingState != TrackingState.Tracking)
                {
                    ArcoreGeospatialIsTracking = false;
                    OnArcoreGeospatialTrackingStatusChanged?.Invoke();

                    _logHelper.LogDebug($"GeoSpatial is not tracking: trackingState = {_arEarthManager.EarthTrackingState}, earthState = {_arEarthManager.EarthState}");
                }
            }

            if (_anchorReadyObjects.Count > 0)
            {
                var anchorReadyObject = _anchorReadyObjects[0];
                anchorReadyObject.OnComplete?.Invoke(anchorReadyObject.Model, anchorReadyObject.Anchor);
                _anchorReadyObjects.Remove(anchorReadyObject);
            }
        }

        public void StopAllAnchorsGettings()
        {
            StopAllCoroutines();

            _anchorReadyObjects.Clear();
            _arModelsWaitingForAnchor.Clear();
        }

        public void GetAnchor(ArModel model, LocationCoordinates userLocation, Action<ArModel, ARGeospatialAnchor> successResult, Action<ArModel, ARGeospatialAnchor> faultResult)
        {
            if (_arModelsWaitingForAnchor.Contains(model.Key))
            {
                return;
            }

            _arModelsWaitingForAnchor.Add(model.Key);
            
            if (model.Anchor == ARObjectAnchorType.terrain)
            {
                ResolveAnchorOnTerrainPromise terrainPromise = 
                    _anchorManager.ResolveAnchorOnTerrainAsync(model.Location.Latitude, model.Location.Longitude, 0, _arEarthManager.CameraGeospatialPose.EunRotation);

                StartCoroutine(GetTerrainAnchor(terrainPromise, model, successResult, faultResult));
                StartCoroutine(GetDefaultAnchorWithDelay(model, faultResult));
            }
            else if (model.Anchor == ARObjectAnchorType.randomTerrain && userLocation != null)
            {
                model.Location = GetRandomLocation(userLocation);

                ResolveAnchorOnTerrainPromise terrainPromise = 
                    _anchorManager.ResolveAnchorOnTerrainAsync(model.Location.Latitude, model.Location.Longitude, 0, _arEarthManager.CameraGeospatialPose.EunRotation);

                StartCoroutine(GetTerrainAnchor(terrainPromise, model, successResult, faultResult));
                StartCoroutine(GetDefaultAnchorWithDelay(model, faultResult));
            }
            else if (model.Anchor ==  ARObjectAnchorType.rooftop)
            {
                ResolveAnchorOnRooftopPromise rooftopPromise =
                    _anchorManager.ResolveAnchorOnRooftopAsync(model.Location.Latitude, model.Location.Longitude, 0, _arEarthManager.CameraGeospatialPose.EunRotation);

                StartCoroutine(GetRooftopAnchor(rooftopPromise, model, successResult, faultResult));
                StartCoroutine(GetDefaultAnchorWithDelay(model, faultResult));
            }
            else if (model.Anchor == ARObjectAnchorType.random && userLocation != null)
            {
                model.Location = GetRandomLocation(userLocation);

                _anchorReadyObjects.Add(new AnchorReadyObject(model, GetDefaultAnchor(model.Location), successResult));
                _arModelsWaitingForAnchor.Remove(model.Key);
            }
            else
            {
                _anchorReadyObjects.Add(new AnchorReadyObject(model, GetDefaultAnchor(model.Location), faultResult));
                _arModelsWaitingForAnchor.Remove(model.Key);
            }
        }
        
        private IEnumerator GetDefaultAnchorWithDelay(ArModel model, Action<ArModel, ARGeospatialAnchor> result)
        {
            yield return _delayForDefaultAnchor;

            if (_arModelsWaitingForAnchor.Contains(model.Key))
            {
                _anchorReadyObjects.Add(new AnchorReadyObject(model, GetDefaultAnchor(model.Location), result));
                _arModelsWaitingForAnchor.Remove(model.Key);
            }
        }

        private ARGeospatialAnchor GetDefaultAnchor(LocationCoordinates location)
        {
            var cameraGeospatialPose = _arEarthManager.CameraGeospatialPose;
            return _anchorManager.AddAnchor(location.Latitude, location.Longitude, cameraGeospatialPose.Altitude, Quaternion.identity);
        }

        private IEnumerator GetTerrainAnchor(ResolveAnchorOnTerrainPromise promise, ArModel model, Action<ArModel, ARGeospatialAnchor> successResult, Action<ArModel, ARGeospatialAnchor> faultResult)
        {
            yield return promise;

            if (!_arModelsWaitingForAnchor.Contains(model.Key))
            {
                yield break;
            }

            var result = promise.Result;

            switch (result.TerrainAnchorState)
            {
                case TerrainAnchorState.Success:
                    if (result.Anchor != null)
                    {
                        _logHelper.LogDebug("GeoSpatial Terrain promise anchor success");
                        _anchorReadyObjects.Add(new AnchorReadyObject(model, result.Anchor, successResult));
                    }
                    else
                    {
                        _logHelper.LogDebug("GeoSpatial Terrain promise anchor null");
                        _anchorReadyObjects.Add(new AnchorReadyObject(model, GetDefaultAnchor(model.Location), faultResult));
                    }
                    break;

                default:
                    _logHelper.LogDebug($"GeoSpatial Terrain promise anchor {result.TerrainAnchorState}");
                    _anchorReadyObjects.Add(new AnchorReadyObject(model, GetDefaultAnchor(model.Location), faultResult));
                    break;
            }

            _arModelsWaitingForAnchor.Remove(model.Key);
        }

        private IEnumerator GetRooftopAnchor(ResolveAnchorOnRooftopPromise promise, ArModel model, Action<ArModel, ARGeospatialAnchor> successResult, Action<ArModel, ARGeospatialAnchor> faultResult)
        {
            yield return promise;

            if (!_arModelsWaitingForAnchor.Contains(model.Key))
            {
                yield break;
            }

            var result = promise.Result;

            switch (result.RooftopAnchorState)
            {
                case RooftopAnchorState.Success:
                    if (result.Anchor != null)
                    {
                        _logHelper.LogDebug("GeoSpatial Rooftop promise anchor success");
                        _anchorReadyObjects.Add(new AnchorReadyObject(model, result.Anchor, successResult));
                    }
                    else
                    {
                        _logHelper.LogDebug("GeoSpatial Rooftop promise anchor null");
                        _anchorReadyObjects.Add(new AnchorReadyObject(model, GetDefaultAnchor(model.Location), faultResult));
                    }
                    break;

                default:
                    _logHelper.LogDebug($"GeoSpatial Rooftop promise anchor {result.RooftopAnchorState}");
                    _anchorReadyObjects.Add(new AnchorReadyObject(model, GetDefaultAnchor(model.Location), faultResult));
                    break;
            }

            _arModelsWaitingForAnchor.Remove(model.Key);
        }

        private System.Random _random = new System.Random();
        private const double earthRadius = 6378137.0;
        private int _quarter = 0;

        private LocationCoordinates GetRandomLocation(LocationCoordinates center)
        {
            double randomDistance = _random.NextDouble() * _randomLocationArea;
            double dx = _random.NextDouble() * (_randomLocationArea / 2);
            double dy = Math.Sqrt((randomDistance * randomDistance) + (dx * dx)); // meters

            if (_quarter == 0)
            {
                // do nothing , first quarter
            }
            else if (_quarter == 1)
            {
                dy = dy * -1;
            }
            else if (_quarter == 2)
            {
                dy *= -1;
                dx *= -1;
            }
            else if (_quarter == 3)
            {
                dx *= -1;
            }

            var lat2 = center.Latitude + (180 / Math.PI) * (dx / earthRadius);
            var lon2 = center.Longitude + (180 / Math.PI) * (dy / earthRadius) / Math.Cos(center.Latitude);
            _quarter = (_quarter + 1) % 4;

            return new LocationCoordinates(lat2, lon2);
        }
    }

    public class AnchorReadyObject
    {
        public AnchorReadyObject(ArModel model, ARGeospatialAnchor anchor, Action<ArModel, ARGeospatialAnchor> onComplete)
        {
            Model = model;
            Anchor = anchor;
            OnComplete = onComplete;
        }

        public ArModel Model { get; set; }
        public ARGeospatialAnchor Anchor { get; set; }
        public Action<ArModel, ARGeospatialAnchor> OnComplete { get; set; }
    }
}
#endif