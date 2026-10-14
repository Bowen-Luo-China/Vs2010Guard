int f(int * restrict p){ return *p; }
int main(void){ int x=0; return f(&x); }
