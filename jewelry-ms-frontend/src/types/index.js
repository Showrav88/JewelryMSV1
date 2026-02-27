// Auth DTOs
export const AuthDTOs = {
  LoginRequest: {
    email: '',
    password: ''
  },
  AuthResponse: {
    token: '',
    user: {}
  }
};

export const RegisterRequest = {
  fullname: '',
  email: '',
  password: '',
  role : '' ,// Default role
  shopId: '' // Optional shop association
  
};

// Product DTOs
// export const ProductDTOs = {
//   ProductCreateRequest: {
//     name: '',
//     category: '',
//     purity: ''
//   },
//   ProductResponse: {
//     id: 0,
//     name: '',
//     price: 0
//   }