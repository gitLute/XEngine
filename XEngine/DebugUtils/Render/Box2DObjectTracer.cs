using Box2D.NET;
using OpenTK.Mathematics;
using XEngine.Core.Graphics.OpenGL;

using static Box2D.NET.B2MathFunction;

namespace XEngine.Core.DebugUtils.Render
{
    /// <summary>
    /// Вспомогательный класс для отрисовки объектов физического мира Box2D.
    /// </summary>
    internal static class Box2DObjectTracer
    {
        /// <summary>
        /// Отрисовывает тело (Body) и все его формы через указанный LineBatcher.
        /// </summary>
        /// <param name="bodyId">Идентификатор тела в Box2D.</param>
        /// <param name="lb">Экземпляр LineBatcher для отрисовки.</param>
        /// <param name="color">Цвет линий.</param>
        public static void TraceBody(B2BodyId bodyId, LineBatcher lb, Vector4 color)
        {
            B2Transform transform = B2Bodies.b2Body_GetTransform(bodyId);

            int shapeCount = B2Bodies.b2Body_GetShapeCount(bodyId);
            Span<B2ShapeId> shapeIds = new B2ShapeId[shapeCount];
            B2Bodies.b2Body_GetShapes(bodyId, shapeIds, shapeCount);

            foreach (var shapeId in shapeIds) TraceShape(shapeId, transform, lb, color);
        }

        private static void TraceShape(B2ShapeId shapeId, B2Transform transform, LineBatcher lb, Vector4 color)
        {
            switch (B2Shapes.b2Shape_GetType(shapeId))
            {
                case B2ShapeType.b2_circleShape:
                    TraceCircle(lb, B2Shapes.b2Shape_GetCircle(shapeId), transform, color);
                    break;
                case B2ShapeType.b2_capsuleShape:
                    TraceCapsule(lb, B2Shapes.b2Shape_GetCapsule(shapeId), transform, color);
                    break;
                case B2ShapeType.b2_segmentShape:
                    TraceSegment(lb, B2Shapes.b2Shape_GetSegment(shapeId), transform, color);
                    break;
                case B2ShapeType.b2_polygonShape:
                    TracePolygon(lb, B2Shapes.b2Shape_GetPolygon(shapeId), transform, color);
                    break;
            }
        }

        private static void CalcArc(ref B2Vec2[] points, float radius, float startAngle, float endAngle)
        {
            float t, ang;
            for (int i = 0; i < points.Length; i++)
            {
                t = (float)i / (points.Length - 1);
                ang = float.Lerp(startAngle, endAngle, t);
                points[i] = b2RotateVector(b2MakeRot(ang), new B2Vec2(radius, 0));
            }
        }

        private static void TraceSegment(LineBatcher lb, B2Segment seg, B2Transform tr, Vector4 color)
        {
            using (lb.TraceLine(closed: false))
            {
                lb.AddPoint(b2TransformPoint(tr, seg.point1), color);
                lb.AddPoint(b2TransformPoint(tr, seg.point2), color);
            }
        }

        private readonly static int PolyRes = 8;
        private static void TracePolygon(LineBatcher lb, B2Polygon poly, B2Transform tr, Vector4 color)
        {
            B2Vec2[] points = new B2Vec2[PolyRes + 1];
            B2Vec2 prev = b2TransformPoint(tr, poly.vertices[poly.count - 1]);
            B2Vec2 curr = b2TransformPoint(tr, poly.vertices[0]);
            B2Vec2 next = b2TransformPoint(tr, poly.vertices[1]);
            GetAngles(prev, curr, next, out float start, out float end);
            CalcArc(ref points, poly.radius, start, end);

            using (lb.TraceLine(closed: true))
            {
                for (int i = 0; i <= PolyRes; i++) lb.AddPoint(points[i] + curr, color);

                for (int i = 1; i < poly.count; i++)
                {
                    prev = curr;
                    curr = next;
                    next = b2TransformPoint(tr, poly.vertices[(i + 1) % poly.count]);
                    GetAngles(prev, curr, next, out start, out end);
                    CalcArc(ref points, poly.radius, start, end);
                    for (int j = 0; j <= PolyRes; j++) lb.AddPoint(points[j] + curr, color);
                }
            }
        }

        private static void GetAngles(B2Vec2 a, B2Vec2 b, B2Vec2 c, out float start, out float end)
        {
            start = B2MathFunction.b2Atan2(b.Y - c.Y, b.X - c.X);
            end = B2MathFunction.b2Atan2(b.Y - a.Y, b.X - a.X);
            if (end < start) end += MathF.Tau;
        }

        private readonly static int CapRes = 8;
        private static void TraceCapsule(LineBatcher lb, B2Capsule capsule, B2Transform tr, Vector4 color)
        {
            float capAng = b2Atan2(
                capsule.center1.Y - capsule.center2.Y,
                capsule.center1.X - capsule.center2.X
            ) - MathF.PI / 2;

            B2Vec2[] points = new B2Vec2[CapRes + 1];
            B2Vec2 c1 = b2TransformPoint(tr, capsule.center1);
            B2Vec2 c2 = b2TransformPoint(tr, capsule.center2);

            using (lb.TraceLine(closed: true))
            {
                CalcArc(ref points, capsule.radius, capAng, capAng + MathF.PI);
                for (int i = 0; i <= CapRes; i++) lb.AddPoint(points[i] + c1, color);
                capAng += MathF.PI;
                CalcArc(ref points, capsule.radius, capAng, capAng + MathF.PI);
                for (int i = 0; i <= CapRes; i++) lb.AddPoint(points[i] + c2, color);
            }
        }

        private readonly static int CircRes = 16;
        private static void TraceCircle(LineBatcher lb, B2Circle circle, B2Transform tr, Vector4 color)
        {
            B2Vec2[] points = new B2Vec2[CircRes + 1];
            B2Vec2 c0 = b2TransformPoint(tr, circle.center);
            CalcArc(ref points, circle.radius, 0, MathF.Tau);
            using (lb.TraceLine(closed: true))
            {
                for (int i = 0; i <= CircRes; i++) lb.AddPoint(points[i] + c0, color);
            }
        }
    }
}