#include <stdio.h>
#include <stdlib.h>

struct Point { int x; int y; };

int main(void)
{
    int a[10] = {1, 2, 3};
    struct Point ps[2] = {
        { .x = 1, .y = 2 },
        { .y = 5 }
    };
    int i;
    for (i = 0; i < 2; i++) {
        printf("%d\n", ps[i].y);
    }
    return 0;
}
