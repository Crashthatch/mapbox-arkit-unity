namespace Mapbox.Unity.Ar
{
	using Mapbox.Unity.Map;
	using Mapbox.Unity.Location;
	using UnityARInterface;
	using UnityEngine;
	using Mapbox.Unity.Utilities;
	using System;

	public class SimpleAutomaticSynchronizationContextBehaviour : MonoBehaviour, ISynchronizationContext
	{
		[SerializeField]
		Transform _arPositionReference;

		[SerializeField]
		AbstractMap _map;

		[SerializeField]
		bool _useAutomaticSynchronizationBias;

		[SerializeField]
		AbstractAlignmentStrategy _alignmentStrategy;

		[SerializeField]
		float _synchronizationBias = 1f;

		[SerializeField]
		float _arTrustRange = 10f;

		[SerializeField]
		float _minimumDeltaDistance = 2f;

		SimpleAutomaticSynchronizationContext _synchronizationContext;

		float _lastHeading;
		float _lastHeight;
		bool _mapIsInitialized = false;
		Location _locationProvidedWhileLoading;
		bool _locationUpdatePending;

		ILocationProvider _locationProvider;

		public event Action<Alignment> OnAlignmentAvailable = delegate { };

		public ILocationProvider LocationProvider
		{
			private get
			{
				if (_locationProvider == null)
				{
					_locationProvider = LocationProviderFactory.Instance.DefaultLocationProvider;
				}

				return _locationProvider;
			}
			set
			{
				if (_locationProvider != null)
				{
					_locationProvider.OnLocationUpdated -= LocationProvider_OnLocationUpdated;

				}
				_locationProvider = value;
				_locationProvider.OnLocationUpdated += LocationProvider_OnLocationUpdated;
			}
		}

		void Start()
		{
			_alignmentStrategy.Register(this);
			_synchronizationContext = new SimpleAutomaticSynchronizationContext();
			_synchronizationContext.MinimumDeltaDistance = _minimumDeltaDistance;
			_synchronizationContext.ArTrustRange = _arTrustRange;
			_synchronizationContext.UseAutomaticSynchronizationBias = _useAutomaticSynchronizationBias;
			_synchronizationContext.SynchronizationBias = _synchronizationBias;
			_synchronizationContext.OnAlignmentAvailable += SynchronizationContext_OnAlignmentAvailable;
			_map.OnInitialized += Map_OnInitialized;
			LocationProvider.OnLocationUpdated += LocationProvider_OnLocationUpdated;
			Debug.Log("SimpleAutomaticSynchronizationContextBehaviour registered LocationProvider_OnLocationUpdated");

			// TODO: not available in ARInterface yet?!
			//UnityARSessionNativeInterface.ARSessionTrackingChangedEvent += UnityARSessionNativeInterface_ARSessionTrackingChanged;
			ARInterface.planeAdded += PlaneAddedHandler;
		}

		void OnDestroy()
		{
			_alignmentStrategy.Unregister(this);
			LocationProvider.OnLocationUpdated -= LocationProvider_OnLocationUpdated;
			ARInterface.planeAdded -= PlaneAddedHandler;
		}

		void Map_OnInitialized()
		{
			Debug.Log("Map initialized. SimpleAutomaticSynchronizationContextBehaviour starting to listen to LocationProvider.OnLocationUpdated...");
			_map.OnInitialized -= Map_OnInitialized;
			_mapIsInitialized = true;

			//Simulate a location update with the location stored while the map was loading.
			if (_locationUpdatePending)
			{
				Debug.Log("Map initialized. Sending pending location update...");
				LocationProvider_OnLocationUpdated(_locationProvidedWhileLoading);
			}

		}

		void PlaneAddedHandler(BoundedPlane plane)
		{
			_lastHeight = plane.center.y;
			Unity.Utilities.Console.Instance.Log(string.Format("AR Plane Height: {0}", _lastHeight), "yellow");
		}

		//void UnityARSessionNativeInterface_ARSessionTrackingChanged(UnityEngine.XR.iOS.UnityARCamera camera)
		//{
		//	Unity.Utilities.Console.Instance.Log(string.Format("AR Tracking State Changed: {0}: {1}", camera.trackingState, camera.trackingReason), "silver");
		//}

		void LocationProvider_OnLocationUpdated(Location location)
		{
			Debug.Log("SimpleAutomaticSynchronizationContextBehaviour.LocationProvider_OnLocationUpdated called");
			// We don't want location updates until we have a map, otherwise our conversion will fail.
			if (_mapIsInitialized)
			{
				if (location.IsLocationUpdated)
				{
					var latitudeLongitude = location.LatitudeLongitude;

					var position = Conversions.GeoToWorldPosition(latitudeLongitude,
																	 _map.CenterMercator,
																	 _map.WorldRelativeScale).ToVector3xz();

					Unity.Utilities.Console.Instance.Log(string.Format("Location: {0},{1}\tAccuracy: {2}\tHeading: {3}\tPosition-in-world: {4}",
																	   latitudeLongitude.x, latitudeLongitude.y, location.Accuracy, location.Heading, position), "lightblue");

					_synchronizationContext.AddSynchronizationNodes(location, position, _arPositionReference.localPosition);
				}
			}
			else
			{
				//Store the received location position so we can issue an initial alignment when the map finishes loading.
				Debug.Log("Map not yet loaded. Storing location until map finishes loading.");
				_locationProvidedWhileLoading = location;
				_locationUpdatePending = true;
			}
		}

		void SynchronizationContext_OnAlignmentAvailable(Ar.Alignment alignment)
		{
			var position = alignment.Position;
			position.y = _lastHeight;
			alignment.Position = position;
			OnAlignmentAvailable(alignment);
		}
	}
}