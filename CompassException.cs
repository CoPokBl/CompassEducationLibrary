using System;

namespace CompassApi; 

// compassexception class
class CompassException : Exception {
    public CompassException(string message) : base(message) { }
}