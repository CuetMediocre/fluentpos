import { HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/internal/operators/map';
import { UserApiService } from 'src/app/core/api/identity/user-api.service';
import { IResult } from 'src/app/core/models/wrappers/IResult';
import { PaginatedResult } from 'src/app/core/models/wrappers/PaginatedResult';
import { User } from '../models/user';
import { UserParams } from '../models/userParams';

@Injectable()
export class UserService {
  constructor(private api: UserApiService) {}

  getUsers(UserParams: UserParams): Observable<PaginatedResult<User>> {
    let params = new HttpParams();
    if (UserParams.searchString)
      params = params.append('searchString', UserParams.searchString);
    if (UserParams.pageNumber)
      params = params.append('pageNumber', UserParams.pageNumber.toString());
    if (UserParams.pageSize)
      params = params.append('pageSize', UserParams.pageSize.toString());
    if (UserParams.orderBy)
      params = params.append('orderBy', UserParams.orderBy.toString());
    return this.api
      .getAlls(params)
      .pipe(map((response: PaginatedResult<User>) => response));
  }

  getUserById(id: string): Observable<User> {
    return this.api.getById(id).pipe(map((response: User) => response));
  }

  createUser(User: User): Observable<IResult<User>> {
    return this.api
      .create(User)
      .pipe(map((response: IResult<User>) => response));
  }

  updateUser(User: User): Observable<IResult<User>> {
    return this.api
      .update(User)
      .pipe(map((response: IResult<User>) => response));
  }

  deleteUser(id: string): Observable<IResult<string>> {
    return this.api
      .delete(id)
      .pipe(map((response: IResult<string>) => response));
  }
}
