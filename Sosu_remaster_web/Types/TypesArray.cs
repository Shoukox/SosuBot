namespace Sosu.Types
{
    public class TypesArray<T1, T2>
    {
        public T1 object0 { get; set; }
        public T2 object1 { get; set; }

        public TypesArray(T1 object0, T2 object1)
        {
            this.object0 = object0;
            this.object1 = object1;
        }
        public bool Contains(object item)
        {
            if (item is T1)
            {
                if (item == (object)object0)
                    return true;
            }
            else if (item is T2)
            {
                if (item == (object)object1)
                    return true;
            }
            return false;
        }
    }
    public class TypesArray<T1, T2, T3>
    {
        public T1 object0 { get; set; }
        public T2 object1 { get; set; }
        public T3 object2 { get; set; }

        public TypesArray(T1 object0, T2 object1, T3 object2)
        {
            this.object0 = object0;
            this.object1 = object1;
            this.object2 = object2;
        }
        public bool Contains(object item)
        {
            if (item is T1)
            {
                if (item == (object)object0)
                    return true;
            }
            else if (item is T2)
            {
                if (item == (object)object1)
                    return true;
            }
            else if (item is T3)
            {
                if (item == (object)object2)
                    return true;
            }
            return false;
        }
    }
    public class TypesArray<T1, T2, T3, T4>
    {
        public T1 object0 { get; set; }
        public T2 object1 { get; set; }
        public T3 object2 { get; set; }
        public T4 object3 { get; set; }

        public TypesArray(T1 object0, T2 object1, T3 object2, T4 object3)
        {
            this.object0 = object0;
            this.object1 = object1;
            this.object2 = object2;
            this.object3 = object3;
        }
        public bool Contains(object item)
        {
            if (item is T1)
            {
                if (item == (object)object0)
                    return true;
            }
            else if (item is T2)
            {
                if (item == (object)object1)
                    return true;
            }
            else if (item is T3)
            {
                if (item == (object)object2)
                    return true;
            }
            else if (item is T4)
            {
                if (item == (object)object3)
                    return true;
            }
            return false;
        }
    }
    public class TypesArray<T1, T2, T3, T4, T5>
    {
        public T1 object0 { get; set; }
        public T2 object1 { get; set; }
        public T3 object2 { get; set; }
        public T4 object3 { get; set; }
        public T5 object4 { get; set; }

        public TypesArray(T1 object0, T2 object1, T3 object2, T4 object3, T5 object4)
        {
            this.object0 = object0;
            this.object1 = object1;
            this.object2 = object2;
            this.object3 = object3;
            this.object4 = object4;
        }
        public bool Contains(object item)
        {
            if (item is T1)
            {
                if (item == (object)object0)
                    return true;
            }
            else if (item is T2)
            {
                if (item == (object)object1)
                    return true;
            }
            else if (item is T3)
            {
                if (item == (object)object2)
                    return true;
            }
            else if (item is T4)
            {
                if (item == (object)object3)
                    return true;
            }
            else if (item is T5)
            {
                if (item == (object)object4)
                    return true;
            }
            return false;
        }
    }
}
