#include <stdio.h>
#include <stdbool.h>          /* VS2010: fatal error C1083 —— 2010 没有这个头文件 */

struct Student { char name[20]; int score; };

int main(void)
{
    int n = 0;
    printf("输入一个整数: ");
    scanf("%d", &n);

    for (int i = 0; i < 3; i++) {          /* VS2010: error C2143（for 里不许声明变量）*/
        printf("i = %d\n", i);
    }

    int total = n * 2;                     /* VS2010: error C2143（声明写在语句后面）*/
    bool ok = true;                        /* bool 来自 stdbool.h，2010 上连头文件都没有 */

    struct Student s = { .score = 90 };    /* VS2010: error C2059（指定初始化器）*/

    char buf[32];
    snprintf(buf, sizeof buf, "%d", total);/* VS2010: 编译过，链接报 LNK2019 */

    printf("total = %d, ok = %d\n", total, ok ? 1 : 0);
    printf("buf = %s, sizeof(int) = %zu\n", buf, sizeof(int));  /* VS2010: %zu 会原样打印出 zu */
    (void)s;
    return 0;
}
