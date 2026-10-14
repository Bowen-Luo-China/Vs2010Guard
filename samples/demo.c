#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>

struct Point { int x; int y; };

int main(void)
{
    int n = 5;
    printf("n = %d\n", n);

    int sum = 0;              /* C89: 声明不能出现在语句之后 */
    long long big = 1234567890123LL;
    printf("big = %lld\n", big);

    char buf[64];
    snprintf(buf, sizeof buf, "%d", n);

    double r = round(1.5);
    int grid[n];

    struct Point p = { .x = 1, .y = 2 };
    bool flag = true;
    printf("%zu %s\n", sizeof(buf), flag ? "yes" : "no");
    return 0;
}