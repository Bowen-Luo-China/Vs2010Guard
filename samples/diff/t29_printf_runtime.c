#include <stdio.h>
int main(void){
    long long v = 1234567890123LL;
    printf("lld=[%lld]\n", v);
    printf("I64d=[%I64d]\n", v);
    printf("zu=[%zu]\n", sizeof(int));
    printf("Iu=[%Iu]\n", (unsigned)sizeof(int));
    return 0;
}
