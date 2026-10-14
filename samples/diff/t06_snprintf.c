#include <stdio.h>
int main(void){ char b[16]; snprintf(b, sizeof b, "%d", 1); return b[0]; }
