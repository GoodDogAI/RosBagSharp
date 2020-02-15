namespace ROS {
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class RosBagException: SystemException {
        public RosBagException() : base() { }
        public RosBagException(string message) : base(message) { }
        public RosBagException(string message, Exception innerException) : base(message, innerException) { }

        protected RosBagException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext) { }
    }
}
