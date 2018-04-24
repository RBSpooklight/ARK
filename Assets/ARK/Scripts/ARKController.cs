using UnityEngine;
using System.Collections;

namespace Kouji.ARK
{
	public class ARKController : MonoBehaviour
	{
		#region Members

		[Header("AR Settings")]
		[SerializeField] private float m_scale = 1f;
		[SerializeField] private bool m_isRenderingBackground = true;
		[SerializeField] private bool m_usePointCloud = true;
		[SerializeField] private bool m_useLightEstimation = true;
		[SerializeField] private ARKInterface.PlaneDetectionMode m_planeDetectionMode;
			
		private Quaternion m_rotation = Quaternion.identity;
		private Quaternion m_invRotation = Quaternion.identity;
		private Vector3 pointOfInterest; //public?
		
		protected ARKInterface m_interface;
		protected Camera m_camera;

		public Camera Camera
		{
			get { return m_camera; }
		}

		public bool IsRunning
		{
			get { return m_interface != null && m_interface.IsRunning; }
		}

		public bool IsRenderingBackground
		{
			get { return m_isRenderingBackground; }
			set
			{
				if (m_interface != null)
					m_interface.IsRenderingBackground = m_isRenderingBackground = value;
			}
		}

		public Quaternion Rotation
		{
			get { return m_rotation; }
			set
			{
				var root = m_camera.transform.parent;
				if (root)
				{
					m_rotation = value;
					m_invRotation = Quaternion.Inverse(m_rotation);

					var poiInRootSpace = root.InverseTransformPoint(pointOfInterest);
					root.localPosition = m_invRotation * (-poiInRootSpace * m_scale) + pointOfInterest;
					root.localRotation = m_invRotation;
				}
			}
		}

		public float Scale
		{
			get { return m_scale; }
			set
			{
				m_scale = value;

				var root = m_camera.transform.parent;
				if (root)
				{
					var poiInRootSpace = root.InverseTransformPoint(pointOfInterest);
					root.localPosition = m_invRotation * (-poiInRootSpace * m_scale) + pointOfInterest;
				}
			}
		}

		#endregion

		private void Update()
		{
			m_interface.Update();
		}

		private void OnEnable()
		{
			Application.targetFrameRate = 60;
			Screen.sleepTimeout = SleepTimeout.NeverSleep; //Prevent screen to turn off
			Input.simulateMouseWithTouches = true;
			
			if (m_interface == null)
				SetupARInterface();

			//Try get gameobject's camera
			if (m_camera == null)
				m_camera = GetComponent<Camera>();

			//Or fallback to main camera
			if (m_camera == null)
				m_camera = Camera.main;
			
			StopAllCoroutines();
			StartCoroutine(StartRoutine());
		}

		private void OnDisable()
		{
			StopAllCoroutines();
			if (IsRunning)
			{
				m_interface.Stop();

				Application.onBeforeRender -= OnBeforeRender;
			}
		}

		private void OnBeforeRender()
		{
			m_interface.UpdateCamera(m_camera);
			
			var pose = new Pose();
			if (m_interface.TryGetPose(ref pose))
			{
				m_camera.transform.localPosition = pose.position;
				m_camera.transform.localRotation = pose.rotation;

				var root = m_camera.transform.parent;
				if (root != null)
					root.localScale = Vector3.one * Scale;
			}
		}

		private IEnumerator StartRoutine()
		{
			yield return m_interface.Start(GetConfiguration());

			if (IsRunning)
			{
				m_interface.SetupCamera(m_camera);
				m_interface.IsRenderingBackground = IsRenderingBackground;

				Application.onBeforeRender += OnBeforeRender;
			}
			else
				enabled = false;
		}

		protected virtual void SetupARInterface()
		{
			m_interface = ARKInterface.GetInterface();
		}

		public ARKInterface.Configuration GetConfiguration()
		{
			return new ARKInterface.Configuration()
			{
				enablePointCloud = m_usePointCloud,
				enableLightEstimation = m_useLightEstimation,
				mode = m_planeDetectionMode
			};
		}
		
		public void AlignWithPointOfInterest(Vector3 _position)
		{
			var root = m_camera.transform.parent;
			if (root)
			{
				var poiInRootSpace = root.InverseTransformPoint(_position - pointOfInterest);
				root.localPosition = m_invRotation * (-poiInRootSpace * Scale);
			}
		}
	}
}
