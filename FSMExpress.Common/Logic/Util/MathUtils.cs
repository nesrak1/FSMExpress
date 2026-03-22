using Avalonia;
using System.Numerics;

namespace FSMExpress.Common.Logic.Util;
public class MathUtils
{
    public static Matrix4x4 AvaloniaToSystemMatrix(Matrix amatrix)
    {
        return new Matrix4x4(
            (float)amatrix.M11, (float)amatrix.M12, (float)amatrix.M13, 0f,
            (float)amatrix.M21, (float)amatrix.M22, (float)amatrix.M23, 0f,
            (float)amatrix.M31, (float)amatrix.M32, (float)amatrix.M33, 0f,
            0f, 0f, 0f, 0f
        );
    }

    public static Matrix SystemToAvaloniaMatrix(Matrix4x4 smatrix)
    {
        return new Matrix(
            smatrix.M11, smatrix.M12, smatrix.M13,
            smatrix.M21, smatrix.M22, smatrix.M23,
            smatrix.M31, smatrix.M32, smatrix.M33
        );
    }
}
