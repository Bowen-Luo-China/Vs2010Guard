#include <stdio.h>
#include <stdbool.h>
#include <stdint.h>

struct Student { char name[20]; int score; };

int main(void)
{
    int n;
    printf("how many? ");
    scanf("%d", &n);

    int scores[n];                       /* VLA */

    for (int i = 0; i < n; i++) {        /* for 里声明 */
        scanf("%d", &scores[i]);
    }

    int total = 0;                       /* 声明在语句之后 */
    bool pass = false;

    struct Student s = { .score = 90 };  /* 指定初始化器 */

    long long big = 1234567890123LL;
    printf("total=%d big=%lld\n", total, big);

    char buf[32];
    snprintf(buf, sizeof buf, "%d", total);

    double avg = round(1.5);
    printf("avg=%f size=%zu\n", avg, sizeof(int));
    (void)pass; (void)s;
    return 0;
}
