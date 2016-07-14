namespace minesweeper
{
    /// <summary>
    /// simple 2D vector
    /// </summary>
    class vector
    {

        /// <summary>
        /// x coordinate
        /// </summary>
        public int x
        {
            get;
            private set;
        }

        /// <summary>
        /// y coordinate
        /// </summary>
        public int y
        {
            get;
            private set;
        }

        public vector(int x_, int y_)
        {
            this.x = x_;
            this.y = y_;
        }
    }
}
