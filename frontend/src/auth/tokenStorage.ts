const KEY = 'ims.auth.token';
export const tokenStorage = {
  get: () => sessionStorage.getItem(KEY),
  set: (token: string) => sessionStorage.setItem(KEY, token),
  clear: () => sessionStorage.removeItem(KEY),
};
