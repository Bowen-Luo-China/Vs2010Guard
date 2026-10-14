struct P{int x;int y;};
int main(void){ struct P p = (struct P){1,2}; return p.x; }
